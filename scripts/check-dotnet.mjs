import { lstat, mkdtemp, readdir, realpath, rm } from 'node:fs/promises';
import { spawnSync } from 'node:child_process';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const policyReference = 'AGENTS.md#internal-access-policy';
const defaultRepositoryRoot = fileURLToPath(new URL('../', import.meta.url));
const defaultVerifierProject = {
  projectPath: 'tools/Puntiro.AssemblyPolicy/Puntiro.AssemblyPolicy.csproj',
  assemblyName: 'Puntiro.AssemblyPolicy',
};
const defaultProtectedProjects = [
  {
    projectPath: 'src/Puntiro.Security/Puntiro.Security.csproj',
    assemblyName: 'Puntiro.Security',
  },
  {
    projectPath: 'src/Puntiro.Modules.Identity/Puntiro.Modules.Identity.csproj',
    assemblyName: 'Puntiro.Modules.Identity',
  },
  {
    projectPath: 'src/Puntiro.Modules.Tenancy/Puntiro.Modules.Tenancy.csproj',
    assemblyName: 'Puntiro.Modules.Tenancy',
  },
  {
    projectPath: 'src/Puntiro.Modules.Integrations/Puntiro.Modules.Integrations.csproj',
    assemblyName: 'Puntiro.Modules.Integrations',
  },
  {
    projectPath: 'tools/Puntiro.Provisioning/Puntiro.Provisioning.csproj',
    assemblyName: 'Puntiro.Provisioning',
  },
];
const defaultProjectGraph = [
  {
    projectPath: 'src/Puntiro.Contracts/Puntiro.Contracts.csproj',
    references: [],
  },
  {
    projectPath: 'src/Puntiro.Security/Puntiro.Security.csproj',
    references: [],
  },
  {
    projectPath: 'src/Puntiro.Modules.Identity/Puntiro.Modules.Identity.csproj',
    references: ['src/Puntiro.Security/Puntiro.Security.csproj'],
  },
  {
    projectPath: 'src/Puntiro.Modules.Tenancy/Puntiro.Modules.Tenancy.csproj',
    references: ['src/Puntiro.Security/Puntiro.Security.csproj'],
  },
  {
    projectPath: 'src/Puntiro.Modules.Integrations/Puntiro.Modules.Integrations.csproj',
    references: ['src/Puntiro.Security/Puntiro.Security.csproj'],
  },
  {
    projectPath: 'apps/cloud/Puntiro.Cloud.csproj',
    references: [
      'src/Puntiro.Contracts/Puntiro.Contracts.csproj',
      'src/Puntiro.Modules.Identity/Puntiro.Modules.Identity.csproj',
      'src/Puntiro.Modules.Tenancy/Puntiro.Modules.Tenancy.csproj',
      'src/Puntiro.Modules.Integrations/Puntiro.Modules.Integrations.csproj',
    ],
  },
  {
    projectPath: 'apps/agent/Puntiro.Agent.csproj',
    references: ['src/Puntiro.Contracts/Puntiro.Contracts.csproj'],
  },
  {
    projectPath: 'apps/kiosk-shell/Puntiro.KioskShell.csproj',
    references: ['src/Puntiro.Contracts/Puntiro.Contracts.csproj'],
  },
  {
    projectPath: 'tools/Puntiro.AssemblyPolicy/Puntiro.AssemblyPolicy.csproj',
    references: [],
  },
  {
    projectPath: 'tools/Puntiro.Provisioning/Puntiro.Provisioning.csproj',
    references: [
      'src/Puntiro.Modules.Identity/Puntiro.Modules.Identity.csproj',
      'src/Puntiro.Modules.Tenancy/Puntiro.Modules.Tenancy.csproj',
    ],
  },
  {
    projectPath: 'tests/Puntiro.UnitTests/Puntiro.UnitTests.csproj',
    references: [
      'src/Puntiro.Security/Puntiro.Security.csproj',
      'src/Puntiro.Modules.Identity/Puntiro.Modules.Identity.csproj',
      'src/Puntiro.Modules.Tenancy/Puntiro.Modules.Tenancy.csproj',
      'src/Puntiro.Modules.Integrations/Puntiro.Modules.Integrations.csproj',
      'tools/Puntiro.Provisioning/Puntiro.Provisioning.csproj',
    ],
  },
  {
    projectPath: 'tests/Puntiro.IntegrationTests/Puntiro.IntegrationTests.csproj',
    references: [
      'apps/cloud/Puntiro.Cloud.csproj',
      'src/Puntiro.Modules.Identity/Puntiro.Modules.Identity.csproj',
      'src/Puntiro.Modules.Tenancy/Puntiro.Modules.Tenancy.csproj',
      'src/Puntiro.Modules.Integrations/Puntiro.Modules.Integrations.csproj',
      'tools/Puntiro.Provisioning/Puntiro.Provisioning.csproj',
    ],
  },
];

function commandText(dotnet, args) {
  return [dotnet, ...args].map(argument => JSON.stringify(argument)).join(' ');
}

function runDotnet({ dotnet, args, repositoryRoot, timeout = 600_000 }) {
  const result = spawnSync(dotnet, args, {
    cwd: repositoryRoot,
    encoding: 'utf8',
    maxBuffer: 64 * 1024 * 1024,
    timeout,
    windowsHide: true,
    env: {
      ...process.env,
      DOTNET_CLI_TELEMETRY_OPTOUT: '1',
      DOTNET_NOLOGO: '1',
      DOTNET_SKIP_FIRST_TIME_EXPERIENCE: '1',
    },
  });

  if (result.status !== 0 || result.error) {
    const processResult = result.signal
      ? `terminated by ${result.signal}`
      : `exit status ${result.status ?? 'unknown'}`;
    const details = [processResult, result.error?.message, result.stdout, result.stderr]
      .filter(Boolean)
      .join('\n')
      .trim();
    throw new Error(`command failed: ${commandText(dotnet, args)}\n${details}`);
  }

  return result;
}

function comparablePath(value) {
  let normalized = path.normalize(path.resolve(value));
  if (process.platform === 'darwin') {
    for (const aliasedRoot of ['/private/tmp', '/private/var']) {
      if (normalized === aliasedRoot || normalized.startsWith(`${aliasedRoot}${path.sep}`)) {
        normalized = normalized.slice('/private'.length);
        break;
      }
    }
  }
  return process.platform === 'win32' ? normalized.toLocaleLowerCase('en-US') : normalized;
}

function isStrictlyInside(parent, candidate) {
  const relative = path.relative(comparablePath(parent), comparablePath(candidate));
  return relative !== ''
    && relative !== '..'
    && !relative.startsWith(`..${path.sep}`)
    && !path.isAbsolute(relative);
}

function parseEvaluatedProperties(stdout, projectPath) {
  try {
    const payload = JSON.parse(stdout.trim());
    if (!payload?.Properties || typeof payload.Properties !== 'object') {
      throw new Error('missing Properties object');
    }
    return payload.Properties;
  } catch (error) {
    throw new Error(`${projectPath}: cannot parse evaluated MSBuild properties: ${error.message}`);
  }
}

function parseEvaluatedProject(stdout, projectPath) {
  try {
    const payload = JSON.parse(stdout.trim());
    if (!payload?.Properties || typeof payload.Properties !== 'object') {
      throw new Error('missing Properties object');
    }
    if (payload.Items !== undefined && typeof payload.Items !== 'object') {
      throw new Error('invalid Items object');
    }
    return payload;
  } catch (error) {
    throw new Error(`${projectPath}: cannot parse evaluated MSBuild graph: ${error.message}`);
  }
}

function displayProjectPath(repositoryRoot, projectPath) {
  const relative = path.relative(comparablePath(repositoryRoot), comparablePath(projectPath));
  if (relative !== ''
    && relative !== '..'
    && !relative.startsWith(`..${path.sep}`)
    && !path.isAbsolute(relative)) {
    return relative.split(path.sep).join('/');
  }
  return path.resolve(projectPath);
}

function validateProjectGraphDefinition(projectGraph, repositoryRoot) {
  const projects = new Map();
  for (const entry of projectGraph) {
    if (!entry.projectPath || !Array.isArray(entry.references)) {
      throw new Error(`project graph entries require projectPath and references (see ${policyReference})`);
    }
    const projectPath = comparablePath(path.resolve(repositoryRoot, entry.projectPath));
    if (projects.has(projectPath)) {
      throw new Error(`duplicate project graph path: ${entry.projectPath} (see ${policyReference})`);
    }
    const references = entry.references.map(reference =>
      comparablePath(path.resolve(repositoryRoot, reference)));
    if (new Set(references).size !== references.length) {
      throw new Error(`duplicate expected ProjectReference in ${entry.projectPath} (see ${policyReference})`);
    }
    projects.set(projectPath, references);
  }

  for (const [projectPath, references] of projects) {
    for (const reference of references) {
      if (!projects.has(reference)) {
        throw new Error(
          `${displayProjectPath(repositoryRoot, projectPath)} expects an unmanaged graph project: ` +
          `${displayProjectPath(repositoryRoot, reference)} (see ${policyReference})`,
        );
      }
    }
  }

  const visited = new Set();
  const visiting = new Set();
  function visit(projectPath) {
    if (visited.has(projectPath)) return;
    if (visiting.has(projectPath)) {
      throw new Error(
        `effective project graph policy contains a dependency cycle at ` +
        `${displayProjectPath(repositoryRoot, projectPath)} (see ${policyReference})`,
      );
    }
    visiting.add(projectPath);
    for (const reference of projects.get(projectPath)) visit(reference);
    visiting.delete(projectPath);
    visited.add(projectPath);
  }
  for (const projectPath of projects.keys()) visit(projectPath);
  return projects;
}

function transitiveReferences(projectPath, projectGraph) {
  const references = new Set();
  function visit(reference) {
    if (references.has(reference)) return;
    references.add(reference);
    for (const nestedReference of projectGraph.get(reference)) visit(nestedReference);
  }
  for (const reference of projectGraph.get(projectPath)) visit(reference);
  return references;
}

export async function validateEffectiveProjectGraph({
  dotnet = process.env.PUNTIRO_DOTNET_BIN ?? 'dotnet',
  repositoryRoot = defaultRepositoryRoot,
  projectGraph = defaultProjectGraph,
  logger = console,
} = {}) {
  const expectedGraph = validateProjectGraphDefinition(projectGraph, repositoryRoot);
  const errors = [];
  const temporaryRoot = await mkdtemp(path.join(os.tmpdir(), 'puntiro-dotnet-graph-'));
  logger.info?.('Evaluating the effective MSBuild ProjectReference graph.');

  try {
    let projectIndex = 0;
    for (const [expectedProjectPath, expectedReferences] of expectedGraph) {
      const projectPath = displayProjectPath(repositoryRoot, expectedProjectPath);
      const artifactsRoot = path.join(
        temporaryRoot,
        `${String(projectIndex).padStart(2, '0')}-${path.basename(projectPath, '.csproj')}`,
      );
      projectIndex += 1;
      runDotnet({
        dotnet,
        repositoryRoot,
        args: [
          'restore',
          projectPath,
          '--use-lock-file',
          '--force-evaluate',
          '--artifacts-path', artifactsRoot,
          `-property:NuGetLockFilePath=${path.join(artifactsRoot, 'packages.lock.json')}`,
          '-property:RestoreLockedMode=false',
        ],
      });
      const result = runDotnet({
        dotnet,
        repositoryRoot,
        args: [
          'msbuild',
          projectPath,
          '-property:Configuration=Release',
          `-property:ArtifactsPath=${artifactsRoot}`,
          '-property:UseArtifactsOutput=true',
          '-target:PrepareProjectReferences',
          '-getProperty:MSBuildProjectFullPath,MSBuildToolsPath',
          '-getItem:ProjectReference',
        ],
      });
      const evaluated = parseEvaluatedProject(result.stdout, projectPath);
      const evaluatedProjectPath = evaluated.Properties.MSBuildProjectFullPath;
      if (typeof evaluatedProjectPath !== 'string'
        || comparablePath(evaluatedProjectPath) !== expectedProjectPath) {
        errors.push(
          `${projectPath}: evaluated MSBuildProjectFullPath does not match the requested graph project ` +
          `(see ${policyReference})`,
        );
        continue;
      }
      const msbuildToolsPath = evaluated.Properties.MSBuildToolsPath;
      if (typeof msbuildToolsPath !== 'string' || !path.isAbsolute(msbuildToolsPath)) {
        errors.push(
          `${projectPath}: evaluated MSBuildToolsPath must be absolute (see ${policyReference})`,
        );
        continue;
      }

      const actualReferences = [];
      const allowedTransitiveReferences = transitiveReferences(expectedProjectPath, expectedGraph);
      for (const item of evaluated.Items?.ProjectReference ?? []) {
        if (typeof item?.FullPath !== 'string' || !path.isAbsolute(item.FullPath)) {
          errors.push(
            `${projectPath}: effective ProjectReference lacks an absolute FullPath ` +
            `(see ${policyReference})`,
          );
          continue;
        }
        const reference = comparablePath(item.FullPath);
        const definingProject = item.DefiningProjectFullPath;
        const isSdkGeneratedTransitive = typeof definingProject === 'string'
          && path.isAbsolute(definingProject)
          && isStrictlyInside(msbuildToolsPath, definingProject);
        if (isSdkGeneratedTransitive) {
          if (!allowedTransitiveReferences.has(reference)) {
            errors.push(
              `${projectPath}: unexpected SDK-generated transitive ProjectReference ` +
              `${displayProjectPath(repositoryRoot, reference)} (see ${policyReference})`,
            );
          }
          continue;
        }
        actualReferences.push(reference);
      }
      const actualSet = new Set(actualReferences);
      if (actualSet.size !== actualReferences.length) {
        errors.push(`${projectPath}: duplicate effective ProjectReference (see ${policyReference})`);
      }
      const expectedSet = new Set(expectedReferences);
      for (const reference of expectedReferences) {
        if (!actualSet.has(reference)) {
          errors.push(
            `${projectPath}: missing effective ProjectReference ` +
            `${displayProjectPath(repositoryRoot, reference)} (see ${policyReference})`,
          );
        }
      }
      for (const reference of actualSet) {
        if (!expectedSet.has(reference)) {
          errors.push(
            `${projectPath}: unexpected effective ProjectReference ` +
            `${displayProjectPath(repositoryRoot, reference)} (see ${policyReference})`,
          );
        }
      }
    }
  } finally {
    await rm(temporaryRoot, { force: true, recursive: true });
  }

  if (errors.length > 0) throw new Error(errors.join('\n'));
}

function requireEvaluatedAbsolutePath(properties, propertyName, projectPath) {
  const value = properties[propertyName];
  if (typeof value !== 'string' || value.length === 0 || !path.isAbsolute(value)) {
    throw new Error(
      `${projectPath}: evaluated ${propertyName} must be an absolute path ` +
      `(see ${policyReference})`,
    );
  }
  return value;
}

function validateProjectPlan({
  repositoryRoot,
  projectPath,
  assemblyName,
  artifactsRoot,
  properties,
}) {
  const evaluatedProjectPath = requireEvaluatedAbsolutePath(
    properties,
    'MSBuildProjectFullPath',
    projectPath,
  );
  const evaluatedArtifactsPath = requireEvaluatedAbsolutePath(
    properties,
    'ArtifactsPath',
    projectPath,
  );
  const evaluatedOutputPath = requireEvaluatedAbsolutePath(
    properties,
    'OutputPath',
    projectPath,
  );
  const evaluatedIntermediateOutputPath = requireEvaluatedAbsolutePath(
    properties,
    'IntermediateOutputPath',
    projectPath,
  );
  const evaluatedTargetPath = requireEvaluatedAbsolutePath(
    properties,
    'TargetPath',
    projectPath,
  );
  const expectedProjectPath = comparablePath(path.resolve(repositoryRoot, projectPath));
  if (comparablePath(evaluatedProjectPath) !== expectedProjectPath) {
    throw new Error(
      `${projectPath}: evaluated MSBuildProjectFullPath does not match the requested project ` +
      `(see ${policyReference})`,
    );
  }
  if (properties.AssemblyName !== assemblyName) {
    throw new Error(
      `${projectPath}: expected assembly identity ${assemblyName}; ` +
      `evaluated ${properties.AssemblyName ?? '<missing>'} (see ${policyReference})`,
    );
  }
  if (comparablePath(evaluatedArtifactsPath) !== comparablePath(artifactsRoot)) {
    throw new Error(
      `${projectPath}: evaluated ArtifactsPath escaped its unique project root ` +
      `(see ${policyReference})`,
    );
  }

  const expectedBinRoot = path.join(artifactsRoot, 'bin');
  const expectedObjRoot = path.join(artifactsRoot, 'obj');
  if (!isStrictlyInside(expectedBinRoot, evaluatedOutputPath)) {
    throw new Error(
      `${projectPath}: evaluated OutputPath must stay under its unique artifacts/bin root ` +
      `(see ${policyReference})`,
    );
  }
  if (!isStrictlyInside(expectedObjRoot, evaluatedIntermediateOutputPath)) {
    throw new Error(
      `${projectPath}: evaluated IntermediateOutputPath must stay under its unique artifacts/obj root ` +
      `(see ${policyReference})`,
    );
  }
  if (!isStrictlyInside(evaluatedOutputPath, evaluatedTargetPath)) {
    throw new Error(
      `${projectPath}: evaluated TargetPath must stay under the project's exact OutputPath ` +
      `(see ${policyReference})`,
    );
  }

  return {
    projectPath: expectedProjectPath,
    assemblyName,
    artifactsRoot: comparablePath(artifactsRoot),
    outputPath: comparablePath(evaluatedOutputPath),
    intermediateOutputPath: comparablePath(evaluatedIntermediateOutputPath),
    targetPath: path.resolve(evaluatedTargetPath),
  };
}

function evaluateProjectPlan({
  dotnet,
  repositoryRoot,
  projectPath,
  assemblyName,
  artifactsRoot,
}) {
  const result = runDotnet({
    dotnet,
    repositoryRoot,
    args: [
      'msbuild',
      projectPath,
      '-property:Configuration=Release',
      `-property:ArtifactsPath=${artifactsRoot}`,
      '-property:UseArtifactsOutput=true',
      '-getProperty:MSBuildProjectFullPath,AssemblyName,TargetPath,OutputPath,IntermediateOutputPath,ArtifactsPath',
    ],
  });
  return validateProjectPlan({
    repositoryRoot,
    projectPath,
    assemblyName,
    artifactsRoot,
    properties: parseEvaluatedProperties(result.stdout, projectPath),
  });
}

async function rejectLinkedArtifactTree(artifactPath) {
  const entry = await lstat(artifactPath);
  if (entry.isSymbolicLink()) {
    throw new Error(
      `${artifactPath}: isolated artifacts must not contain a symbolic link or reparse point ` +
      `(see ${policyReference})`,
    );
  }
  if (!entry.isDirectory()) return;
  for (const child of await readdir(artifactPath)) {
    await rejectLinkedArtifactTree(path.join(artifactPath, child));
  }
}

async function requireRealArtifactPath(artifactsRoot, artifactPath, kind) {
  const resolvedRoot = await realpath(artifactsRoot);
  const resolvedArtifact = await realpath(artifactPath);
  if (comparablePath(resolvedRoot) !== comparablePath(artifactsRoot)
    || comparablePath(resolvedArtifact) !== comparablePath(artifactPath)) {
    throw new Error(
      `${artifactPath}: isolated ${kind} resolved through a symbolic link or reparse point ` +
      `(see ${policyReference})`,
    );
  }
  if (comparablePath(resolvedArtifact) !== comparablePath(resolvedRoot)
    && !isStrictlyInside(resolvedRoot, resolvedArtifact)) {
    throw new Error(
      `${artifactPath}: isolated ${kind} escaped its real artifacts root ` +
      `(see ${policyReference})`,
    );
  }
  return lstat(artifactPath);
}

async function requireSafeArtifactTree(artifactsRoot) {
  await rejectLinkedArtifactTree(artifactsRoot);
  const root = await requireRealArtifactPath(artifactsRoot, artifactsRoot, 'root');
  if (!root.isDirectory()) {
    throw new Error(`${artifactsRoot}: isolated artifacts root must be a directory (see ${policyReference})`);
  }
}

async function requireProducedTarget(plan) {
  try {
    await lstat(plan.targetPath);
  } catch {
    throw new Error(
      `${plan.projectPath}: isolated build did not produce expected target ${plan.targetPath} ` +
      `(see ${policyReference})`,
    );
  }
  await requireSafeArtifactTree(plan.artifactsRoot);
  const output = await requireRealArtifactPath(plan.artifactsRoot, plan.outputPath, 'output path');
  const intermediate = await requireRealArtifactPath(
    plan.artifactsRoot,
    plan.intermediateOutputPath,
    'intermediate path',
  );
  const target = await requireRealArtifactPath(plan.artifactsRoot, plan.targetPath, 'target');
  if (!output.isDirectory() || !intermediate.isDirectory() || !target.isFile()) {
    throw new Error(
      `${plan.projectPath}: isolated build did not produce expected target ${plan.targetPath} ` +
      `(see ${policyReference})`,
    );
  }
}

export async function buildProjectInIsolatedArtifacts({
  dotnet = process.env.PUNTIRO_DOTNET_BIN ?? 'dotnet',
  repositoryRoot = defaultRepositoryRoot,
  projectPath,
  assemblyName,
  artifactsRoot,
  logger = console,
}) {
  logger.info?.(`Restoring ${projectPath} into isolated artifacts: ${artifactsRoot}`);
  runDotnet({
    dotnet,
    repositoryRoot,
    args: ['restore', projectPath, '--locked-mode', '--artifacts-path', artifactsRoot],
  });
  const planBeforeBuild = evaluateProjectPlan({
    dotnet,
    repositoryRoot,
    projectPath,
    assemblyName,
    artifactsRoot,
  });

  runDotnet({
    dotnet,
    repositoryRoot,
    args: [
      'build',
      projectPath,
      '--configuration', 'Release',
      '--no-restore',
      '--disable-build-servers',
      '-m:1',
      '--artifacts-path', artifactsRoot,
    ],
  });
  await requireProducedTarget(planBeforeBuild);

  const planAfterBuild = evaluateProjectPlan({
    dotnet,
    repositoryRoot,
    projectPath,
    assemblyName,
    artifactsRoot,
  });
  if (JSON.stringify(planAfterBuild) !== JSON.stringify(planBeforeBuild)) {
    throw new Error(
      `${projectPath}: evaluated target plan changed across the isolated build ` +
      `(see ${policyReference})`,
    );
  }

  return planAfterBuild.targetPath;
}

async function buildSolutionInIsolatedArtifacts({
  dotnet,
  repositoryRoot,
  solutionPath,
  artifactsRoot,
  logger,
}) {
  logger.info?.(`Restoring the locked solution into isolated artifacts: ${artifactsRoot}`);
  runDotnet({
    dotnet,
    repositoryRoot,
    args: ['restore', solutionPath, '--locked-mode', '--artifacts-path', artifactsRoot],
  });
  runDotnet({
    dotnet,
    repositoryRoot,
    args: [
      'build',
      solutionPath,
      '--configuration', 'Release',
      '--no-restore',
      '--disable-build-servers',
      '-m:1',
      '--artifacts-path', artifactsRoot,
    ],
  });
  await requireSafeArtifactTree(artifactsRoot);
}

function safeArtifactLabel(index, assemblyName) {
  const safeName = assemblyName.replaceAll(/[^A-Za-z0-9_.-]/g, '_');
  return `${String(index).padStart(2, '0')}-${safeName}`;
}

function validateProtectedProjects(protectedProjects, repositoryRoot) {
  const names = new Set();
  const paths = new Set();
  for (const project of protectedProjects) {
    if (!project.projectPath || !project.assemblyName) {
      throw new Error(`protected project entries require projectPath and assemblyName (see ${policyReference})`);
    }
    if (!names.add(project.assemblyName)) {
      throw new Error(
        `duplicate protected assembly identity: ${project.assemblyName} (see ${policyReference})`,
      );
    }
    const projectPath = comparablePath(path.resolve(repositoryRoot, project.projectPath));
    if (!paths.add(projectPath)) {
      throw new Error(`duplicate protected project path: ${project.projectPath} (see ${policyReference})`);
    }
  }
}

export async function runDotnetPolicy({
  dotnet = process.env.PUNTIRO_DOTNET_BIN ?? 'dotnet',
  repositoryRoot = defaultRepositoryRoot,
  solutionPath = 'Puntiro.slnx',
  protectedProjects = defaultProtectedProjects,
  verifierProject = defaultVerifierProject,
  projectGraph = defaultProjectGraph,
  logger = console,
} = {}) {
  validateProtectedProjects(protectedProjects, repositoryRoot);
  await validateEffectiveProjectGraph({ dotnet, repositoryRoot, projectGraph, logger });
  const temporaryRoot = await mkdtemp(path.join(os.tmpdir(), 'puntiro-dotnet-check-'));

  try {
    await buildSolutionInIsolatedArtifacts({
      dotnet,
      repositoryRoot,
      solutionPath,
      artifactsRoot: path.join(temporaryRoot, '00-solution'),
      logger,
    });
    const verifierPath = await buildProjectInIsolatedArtifacts({
      dotnet,
      repositoryRoot,
      projectPath: verifierProject.projectPath,
      assemblyName: verifierProject.assemblyName,
      artifactsRoot: path.join(temporaryRoot, safeArtifactLabel(1, verifierProject.assemblyName)),
      logger,
    });
    const protectedOutputs = [];
    for (const [index, project] of protectedProjects.entries()) {
      protectedOutputs.push({
        assemblyName: project.assemblyName,
        targetPath: await buildProjectInIsolatedArtifacts({
          dotnet,
          repositoryRoot,
          projectPath: project.projectPath,
          assemblyName: project.assemblyName,
          artifactsRoot: path.join(
            temporaryRoot,
            safeArtifactLabel(index + 2, project.assemblyName),
          ),
          logger,
        }),
      });
    }

    logger.info?.('Verifying exact project TargetPaths, assembly identities, and friend metadata.');
    runDotnet({
      dotnet,
      repositoryRoot,
      args: [
        verifierPath,
        ...protectedOutputs.flatMap(output => [output.assemblyName, output.targetPath]),
      ],
    });
  } finally {
    await rm(temporaryRoot, { force: true, recursive: true });
  }
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? '').href) {
  try {
    await runDotnetPolicy();
  } catch (error) {
    console.error(error.message);
    process.exitCode = 1;
  }
}
