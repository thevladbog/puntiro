import { mkdtemp, rm, stat } from 'node:fs/promises';
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

async function requireProducedTarget(plan) {
  let target;
  try {
    target = await stat(plan.targetPath);
  } catch {
    target = undefined;
  }
  if (!target?.isFile()) {
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

function buildSolutionInIsolatedArtifacts({
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
  logger = console,
} = {}) {
  validateProtectedProjects(protectedProjects, repositoryRoot);
  const temporaryRoot = await mkdtemp(path.join(os.tmpdir(), 'puntiro-dotnet-check-'));

  try {
    buildSolutionInIsolatedArtifacts({
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
