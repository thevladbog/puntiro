import assert from 'node:assert/strict';
import { access, mkdir, mkdtemp, rm, writeFile } from 'node:fs/promises';
import { spawnSync } from 'node:child_process';
import { after, before, test } from 'node:test';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repositoryRoot = fileURLToPath(new URL('../', import.meta.url));
const verifierProjectPath = path.join(
  repositoryRoot,
  'tools/Puntiro.AssemblyPolicy/Puntiro.AssemblyPolicy.csproj',
);
const dotnet = process.env.PUNTIRO_DOTNET_BIN ?? 'dotnet';
const approvedAssemblyInfo = `using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Puntiro.UnitTests")]
[assembly: InternalsVisibleTo("Puntiro.IntegrationTests")]
`;

let policyModule;
let testRoot;

function runDotnet(args, cwd) {
  return spawnSync(dotnet, args, {
    cwd,
    encoding: 'utf8',
    timeout: 180_000,
    env: {
      ...process.env,
      DOTNET_CLI_TELEMETRY_OPTOUT: '1',
      DOTNET_NOLOGO: '1',
      DOTNET_SKIP_FIRST_TIME_EXPERIENCE: '1',
    },
  });
}

function commandFailure(result) {
  return [result.error?.message, result.stdout, result.stderr].filter(Boolean).join('\n');
}

async function createFixtureRoot(name) {
  const root = path.join(testRoot, name);
  await mkdir(root, { recursive: true });
  await writeFile(path.join(root, 'global.json'), JSON.stringify({
    sdk: {
      version: '10.0.302',
      rollForward: 'disable',
      allowPrerelease: false,
    },
  }));
  await writeFile(path.join(root, 'Directory.Build.props'), `<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  </PropertyGroup>
</Project>
`);
  return root;
}

async function writeProject(root, relativeDirectory, projectName, {
  properties = '',
  projectExtra = '',
  assemblyInfo = approvedAssemblyInfo,
  files = {},
} = {}) {
  const projectDirectory = path.join(root, relativeDirectory);
  await mkdir(path.join(projectDirectory, 'Properties'), { recursive: true });
  await writeFile(path.join(projectDirectory, `${projectName}.csproj`), `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    ${properties}
  </PropertyGroup>
  ${projectExtra}
</Project>
`);
  if (assemblyInfo !== null) {
    await writeFile(path.join(projectDirectory, 'Properties/AssemblyInfo.cs'), assemblyInfo);
  }
  await writeFile(path.join(projectDirectory, 'Marker.cs'), 'public static class Marker;\n');
  await writeFile(path.join(projectDirectory, 'packages.lock.json'), JSON.stringify({
    version: 2,
    dependencies: { 'net10.0': {} },
  }));
  for (const [relativePath, content] of Object.entries(files)) {
    const target = path.join(projectDirectory, relativePath);
    await mkdir(path.dirname(target), { recursive: true });
    await writeFile(target, content);
  }
  return path.posix.join(relativeDirectory, `${projectName}.csproj`);
}

function buildNormally(root, projectPath) {
  const result = runDotnet([
    'build',
    projectPath,
    '--configuration', 'Release',
    '-p:RestoreLockedMode=true',
  ], root);
  assert.equal(result.status, 0, `fixture must compile normally:\n${commandFailure(result)}`);
}

function runPolicy(root, solutionPath, protectedProjects) {
  return policyModule.runDotnetPolicy({
    dotnet,
    repositoryRoot: root,
    solutionPath,
    protectedProjects,
    verifierProject: {
      projectPath: verifierProjectPath,
      assemblyName: 'Puntiro.AssemblyPolicy',
    },
    logger: {},
  });
}

before(async () => {
  policyModule = await import(new URL('./check-dotnet.mjs', import.meta.url).href);
  testRoot = await mkdtemp(path.join(os.tmpdir(), 'puntiro-dotnet-policy-'));
});

after(async () => {
  if (testRoot) await rm(testRoot, { force: true, recursive: true });
});

test('accepts relocated declarations when final identity and friend allowlist are exact', async () => {
  const root = await createFixtureRoot('relocated');
  const projectPath = await writeProject(root, '.', 'RelocatedFriends', {
    projectExtra: `<ItemGroup>
    <Compile Remove="RelocatedAssemblyInfo.cs" />
  </ItemGroup>
  <Target Name="RelocateFriendDeclarations" BeforeTargets="CoreCompile">
    <ItemGroup>
      <Compile Remove="Properties/AssemblyInfo.cs" />
      <Compile Include="RelocatedAssemblyInfo.cs" />
    </ItemGroup>
  </Target>`,
    files: {
      'RelocatedAssemblyInfo.cs': String.raw`[assembly: System.Runtime.CompilerServices.Internals\u0056isibleToAttribute("Puntiro.UnitTests")]
[assembly: System.Runtime.CompilerServices.Internals\u0056isibleToAttribute("Puntiro.IntegrationTests")]
`,
    },
  });

  await assert.doesNotReject(runPolicy(root, projectPath, [{
    projectPath,
    assemblyName: 'RelocatedFriends',
  }]));
});

test('accepts imported MSBuild attributes when final friend allowlist is exact', async () => {
  const root = await createFixtureRoot('imported');
  const projectPath = await writeProject(root, '.', 'ImportedFriends', {
    projectExtra: '<Import Project="Custom.props" />',
    files: {
      'Custom.props': `<Project>
  <ItemGroup>
    <Compile Remove="Properties/AssemblyInfo.cs" />
    <AssemblyAttribute Include="System.Runtime.CompilerServices.Internals&#86;isibleToAttribute">
      <_Parameter1>Puntiro.UnitTests</_Parameter1>
    </AssemblyAttribute>
    <AssemblyAttribute Include="System.Runtime.CompilerServices.Internals&#86;isibleToAttribute">
      <_Parameter1>Puntiro.IntegrationTests</_Parameter1>
    </AssemblyAttribute>
  </ItemGroup>
</Project>
`,
    },
  });

  await assert.doesNotReject(runPolicy(root, projectPath, [{
    projectPath,
    assemblyName: 'ImportedFriends',
  }]));
});

test('rejects build success when only stale normal bin and obj exist', async () => {
  const root = await createFixtureRoot('stale');
  const projectPath = await writeProject(root, '.', 'StaleOutput');
  buildNormally(root, projectPath);
  await access(path.join(root, 'bin/Release/net10.0/StaleOutput.dll'));
  await access(path.join(root, 'obj/project.assets.json'));
  await writeFile(path.join(root, 'Directory.Build.targets'), `<Project>
  <PropertyGroup>
    <BuildDependsOn></BuildDependsOn>
  </PropertyGroup>
</Project>
`);

  await assert.rejects(
    runPolicy(root, projectPath, [{ projectPath, assemblyName: 'StaleOutput' }]),
    /isolated build did not produce expected target .*StaleOutput\.dll/,
  );
});

test('does not substitute an approved unprotected collision DLL for the protected output', async () => {
  const root = await createFixtureRoot('collision');
  const protectedProject = await writeProject(root, 'Protected', 'Protected', {
    assemblyInfo: `${approvedAssemblyInfo}[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Unapproved.Tests")]\n`,
  });
  const collisionProject = await writeProject(root, 'Collision', 'Collision', {
    properties: '<AssemblyName>Protected</AssemblyName>',
  });
  buildNormally(root, collisionProject);
  await access(path.join(root, 'Collision/bin/Release/net10.0/Protected.dll'));

  await assert.rejects(
    runPolicy(root, protectedProject, [{
      projectPath: protectedProject,
      assemblyName: 'Protected',
    }]),
    /Unapproved\.Tests/,
  );
});
