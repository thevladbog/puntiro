import { mkdtemp, readdir, rm } from 'node:fs/promises';
import { spawnSync } from 'node:child_process';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const policyReference = 'AGENTS.md#internal-access-policy';
const friendAttributeName = 'System.Runtime.CompilerServices.InternalsVisibleTo';
const defaultRepositoryRoot = fileURLToPath(new URL('../', import.meta.url));
const defaultProtectedProjects = [
  {
    projectPath: 'src/Puntiro.Security/Puntiro.Security.csproj',
    canonicalSource: 'Properties/AssemblyInfo.cs',
    assemblyName: 'Puntiro.Security',
  },
  {
    projectPath: 'src/Puntiro.Modules.Identity/Puntiro.Modules.Identity.csproj',
    canonicalSource: 'Properties/AssemblyInfo.cs',
    assemblyName: 'Puntiro.Modules.Identity',
  },
  {
    projectPath: 'src/Puntiro.Modules.Tenancy/Puntiro.Modules.Tenancy.csproj',
    canonicalSource: 'Properties/AssemblyInfo.cs',
    assemblyName: 'Puntiro.Modules.Tenancy',
  },
  {
    projectPath: 'src/Puntiro.Modules.Integrations/Puntiro.Modules.Integrations.csproj',
    canonicalSource: 'Properties/AssemblyInfo.cs',
    assemblyName: 'Puntiro.Modules.Integrations',
  },
  {
    projectPath: 'tools/Puntiro.Provisioning/Puntiro.Provisioning.csproj',
    canonicalSource: 'Properties/AssemblyInfo.cs',
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
    const details = [result.error?.message, result.stdout, result.stderr]
      .filter(Boolean)
      .join('\n')
      .trim();
    throw new Error(
      `command failed: ${commandText(dotnet, args)}${details ? `\n${details}` : ''}`,
    );
  }

  return result;
}

function comparablePath(value) {
  const normalized = path.normalize(path.resolve(value));
  return process.platform === 'win32' ? normalized.toLocaleLowerCase('en-US') : normalized;
}

function decodeIdentifierEscapes(value) {
  return value
    .replace(/\\U([0-9a-fA-F]{8})/g, (_, codePoint) => String.fromCodePoint(Number.parseInt(codePoint, 16)))
    .replace(/\\u([0-9a-fA-F]{4})/g, (_, codePoint) => String.fromCodePoint(Number.parseInt(codePoint, 16)));
}

function normalizedAttributeName(value) {
  const decoded = decodeIdentifierEscapes(String(value ?? ''))
    .replaceAll(/\s/g, '')
    .replace(/^global::/, '');
  return decoded.endsWith('Attribute') ? decoded.slice(0, -'Attribute'.length) : decoded;
}

function parseEvaluatedItems(stdout, projectPath) {
  try {
    const payload = JSON.parse(stdout.trim());
    if (!payload?.Items || typeof payload.Items !== 'object') throw new Error('missing Items object');
    return payload.Items;
  } catch (error) {
    throw new Error(`${projectPath}: cannot parse evaluated MSBuild items: ${error.message}`);
  }
}

export async function inspectProtectedProjectInputs({
  dotnet = process.env.PUNTIRO_DOTNET_BIN ?? 'dotnet',
  repositoryRoot = defaultRepositoryRoot,
  projectPath,
  canonicalSource,
  baseOutputPath,
}) {
  const projectDirectory = path.dirname(path.resolve(repositoryRoot, projectPath));
  const canonicalPath = comparablePath(path.resolve(projectDirectory, canonicalSource));
  const outputPath = `${path.resolve(baseOutputPath)}${path.sep}`;
  const result = runDotnet({
    dotnet,
    repositoryRoot,
    args: [
      'msbuild',
      projectPath,
      '-property:Configuration=Release',
      `-property:BaseOutputPath=${outputPath}`,
      '-target:GetAssemblyAttributes',
      '-getItem:Compile,AssemblyAttribute,AssemblyAttributes,InternalsVisibleTo',
    ],
  });
  const items = parseEvaluatedItems(result.stdout, projectPath);
  const compileItems = Array.isArray(items.Compile) ? items.Compile : [];
  const canonicalCount = compileItems.filter(item => {
    const fullPath = item.FullPath
      ? item.FullPath
      : path.resolve(projectDirectory, String(item.Identity ?? ''));
    return comparablePath(fullPath) === canonicalPath;
  }).length;
  const generatedFriendAttributes = (Array.isArray(items.AssemblyAttribute)
    ? items.AssemblyAttribute
    : []).filter(item => normalizedAttributeName(item.Identity) === friendAttributeName);
  const legacyFriendAttributes = (Array.isArray(items.AssemblyAttributes)
    ? items.AssemblyAttributes
    : []).filter(item => normalizedAttributeName(item.Identity) === friendAttributeName);
  const friendItems = Array.isArray(items.InternalsVisibleTo) ? items.InternalsVisibleTo : [];
  const errors = [];

  if (canonicalCount !== 1) {
    errors.push(
      `${projectPath}: ${canonicalSource} must be an effective Compile item exactly once ` +
      `(received ${canonicalCount}; see ${policyReference})`,
    );
  }
  if (generatedFriendAttributes.length > 0) {
    errors.push(
      `${projectPath}: effective AssemblyAttribute items must not generate InternalsVisibleTo; ` +
      `received ${generatedFriendAttributes.length} (see ${policyReference})`,
    );
  }
  if (legacyFriendAttributes.length > 0) {
    errors.push(
      `${projectPath}: effective AssemblyAttributes items must not generate InternalsVisibleTo; ` +
      `received ${legacyFriendAttributes.length} (see ${policyReference})`,
    );
  }
  if (friendItems.length > 0) {
    errors.push(
      `${projectPath}: effective InternalsVisibleTo items are forbidden; ` +
      `received ${friendItems.length} (see ${policyReference})`,
    );
  }

  return errors;
}

async function filesNamed(root, fileName) {
  let entries;
  try {
    entries = await readdir(root, { withFileTypes: true });
  } catch (error) {
    if (error.code === 'ENOENT') return [];
    throw error;
  }

  const matches = [];
  for (const entry of entries) {
    const entryPath = path.join(root, entry.name);
    if (entry.isDirectory()) {
      matches.push(...await filesNamed(entryPath, fileName));
    } else if (entry.isFile() && entry.name === fileName) {
      matches.push(entryPath);
    }
  }
  return matches;
}

export async function buildFreshOutputs({
  dotnet = process.env.PUNTIRO_DOTNET_BIN ?? 'dotnet',
  repositoryRoot = defaultRepositoryRoot,
  solutionPath = 'Puntiro.slnx',
  protectedAssemblyNames,
  verifierAssemblyName = 'Puntiro.AssemblyPolicy',
  beforeBuild,
  logger = console,
}) {
  const temporaryRoot = await mkdtemp(path.join(os.tmpdir(), 'puntiro-dotnet-check-'));
  const outputRoot = path.join(temporaryRoot, 'output');
  const baseOutputPath = `${outputRoot}${path.sep}`;

  try {
    if (beforeBuild) await beforeBuild(outputRoot);
    logger.info?.(`Building Release assemblies into unique output root: ${outputRoot}`);
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
        `-p:BaseOutputPath=${baseOutputPath}`,
      ],
    });

    const outputByAssembly = new Map();
    const expectedAssemblyNames = [...new Set([
      ...protectedAssemblyNames,
      verifierAssemblyName,
    ])];
    for (const assemblyName of expectedAssemblyNames) {
      const fileName = `${assemblyName}.dll`;
      const matches = await filesNamed(outputRoot, fileName);
      if (matches.length !== 1) {
        throw new Error(
          `fresh build must produce exactly one ${fileName} in its unique output root; ` +
          `received ${matches.length}`,
        );
      }
      outputByAssembly.set(assemblyName, matches[0]);
    }

    const verifier = outputByAssembly.get(verifierAssemblyName);
    const protectedAssemblies = protectedAssemblyNames.map(name => outputByAssembly.get(name));
    logger.info?.('Verifying metadata from the exact assemblies produced by this build.');
    runDotnet({
      dotnet,
      repositoryRoot,
      args: [verifier, ...protectedAssemblies],
    });
  } finally {
    await rm(temporaryRoot, { force: true, recursive: true });
  }
}

export async function runDotnetPolicy({
  dotnet = process.env.PUNTIRO_DOTNET_BIN ?? 'dotnet',
  repositoryRoot = defaultRepositoryRoot,
  solutionPath = 'Puntiro.slnx',
  protectedProjects = defaultProtectedProjects,
  verifierAssemblyName = 'Puntiro.AssemblyPolicy',
  logger = console,
} = {}) {
  logger.info?.('Restoring the locked .NET dependency graph.');
  runDotnet({
    dotnet,
    repositoryRoot,
    args: ['restore', solutionPath, '--locked-mode'],
  });

  await buildFreshOutputs({
    dotnet,
    repositoryRoot,
    solutionPath,
    protectedAssemblyNames: protectedProjects.map(project => project.assemblyName),
    verifierAssemblyName,
    logger,
    beforeBuild: async outputRoot => {
      const errors = [];
      for (const project of protectedProjects) {
        errors.push(...await inspectProtectedProjectInputs({
          dotnet,
          repositoryRoot,
          projectPath: project.projectPath,
          canonicalSource: project.canonicalSource,
          baseOutputPath: outputRoot,
        }));
      }
      if (errors.length > 0) throw new Error(errors.join('\n'));
      logger.info?.('Evaluated MSBuild inputs preserve canonical internal-access provenance.');
    },
  });
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? '').href) {
  try {
    await runDotnetPolicy();
  } catch (error) {
    console.error(error.message);
    process.exitCode = 1;
  }
}
