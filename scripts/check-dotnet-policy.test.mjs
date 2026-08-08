import assert from 'node:assert/strict';
import { access, mkdir, mkdtemp, rm, writeFile } from 'node:fs/promises';
import { spawnSync } from 'node:child_process';
import { after, before, test } from 'node:test';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repositoryRoot = fileURLToPath(new URL('../', import.meta.url));
const dotnet = process.env.PUNTIRO_DOTNET_BIN ?? 'dotnet';
const policyModuleUrl = new URL('./check-dotnet.mjs', import.meta.url);
const approvedAssemblyInfo = `using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Puntiro.UnitTests")]
[assembly: InternalsVisibleTo("Puntiro.IntegrationTests")]
`;

let policyModule;
let testRoot;
let verifierDll;

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

async function loadPolicyModule() {
  try {
    return await import(policyModuleUrl.href);
  } catch (error) {
    assert.fail(`check-dotnet policy module must be loadable: ${error.message}`);
  }
}

async function writeSdkFiles(root) {
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
  </PropertyGroup>
</Project>
`);
}

async function createProtectedProject(name, { customProps, relocatedSource } = {}) {
  const root = path.join(testRoot, name);
  await mkdir(path.join(root, 'Properties'), { recursive: true });
  await writeSdkFiles(root);
  await writeFile(path.join(root, `${name}.csproj`), `<Project Sdk="Microsoft.NET.Sdk">
  <Import Project="Custom.props" Condition="Exists('Custom.props')" />
</Project>
`);
  await writeFile(path.join(root, 'Properties/AssemblyInfo.cs'), approvedAssemblyInfo);
  await writeFile(path.join(root, 'Marker.cs'), 'public static class Marker;\n');
  if (customProps) await writeFile(path.join(root, 'Custom.props'), customProps);
  if (relocatedSource) await writeFile(path.join(root, 'RelocatedAssemblyInfo.cs'), relocatedSource);
  return root;
}

function buildProject(root, name) {
  const result = runDotnet([
    'build',
    `${name}.csproj`,
    '--configuration', 'Release',
    '-p:RestorePackagesWithLockFile=false',
  ], root);
  assert.equal(result.status, 0, `scratch project must compile:\n${commandFailure(result)}`);
  return path.join(root, 'bin/Release/net10.0', `${name}.dll`);
}

function verifyMetadata(assemblyPath) {
  const result = runDotnet([verifierDll, assemblyPath], repositoryRoot);
  assert.equal(
    result.status,
    0,
    `relocated exact declarations prove the metadata-only check can pass:\n${commandFailure(result)}`,
  );
}

before(async () => {
  policyModule = await loadPolicyModule();
  testRoot = await mkdtemp(path.join(os.tmpdir(), 'puntiro-dotnet-policy-'));
  const verifierOutput = path.join(testRoot, 'verifier');
  const verifierArtifacts = path.join(testRoot, 'verifier-artifacts');
  const result = runDotnet([
    'build',
    path.join(repositoryRoot, 'tools/Puntiro.AssemblyPolicy/Puntiro.AssemblyPolicy.csproj'),
    '--configuration', 'Release',
    '--output', verifierOutput,
    '--artifacts-path', verifierArtifacts,
    '-p:RestoreLockedMode=true',
  ], repositoryRoot);
  assert.equal(result.status, 0, `failed to build metadata verifier:\n${commandFailure(result)}`);
  verifierDll = path.join(verifierOutput, 'Puntiro.AssemblyPolicy.dll');
});

after(async () => {
  if (testRoot) await rm(testRoot, { force: true, recursive: true });
});

test('rejects a real compiled relocation of the exact friend declarations', async () => {
  const name = 'RelocatedFriends';
  const root = await createProtectedProject(name, {
    customProps: `<Project>
  <ItemGroup>
    <Compile Remove="Properties/AssemblyInfo.cs" />
  </ItemGroup>
</Project>
`,
    relocatedSource: String.raw`[assembly: System.Runtime.CompilerServices.Internals\u0056isibleToAttribute("Puntiro.UnitTests")]
[assembly: System.Runtime.CompilerServices.Internals\u0056isibleToAttribute("Puntiro.IntegrationTests")]
`,
  });
  verifyMetadata(buildProject(root, name));

  const errors = await policyModule.inspectProtectedProjectInputs({
    dotnet,
    repositoryRoot: root,
    projectPath: `${name}.csproj`,
    canonicalSource: 'Properties/AssemblyInfo.cs',
    baseOutputPath: path.join(root, 'fresh-output'),
  });

  assert.deepEqual(errors, [
    `${name}.csproj: Properties/AssemblyInfo.cs must be an effective Compile item exactly once (received 0; see AGENTS.md#internal-access-policy)`,
  ]);
});

test('rejects exact friend declarations recreated by an arbitrary imported MSBuild file', async () => {
  const name = 'ImportedFriends';
  const root = await createProtectedProject(name, {
    customProps: `<Project>
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
  });
  verifyMetadata(buildProject(root, name));

  const errors = await policyModule.inspectProtectedProjectInputs({
    dotnet,
    repositoryRoot: root,
    projectPath: `${name}.csproj`,
    canonicalSource: 'Properties/AssemblyInfo.cs',
    baseOutputPath: path.join(root, 'fresh-output'),
  });

  assert.deepEqual(errors, [
    `${name}.csproj: Properties/AssemblyInfo.cs must be an effective Compile item exactly once (received 0; see AGENTS.md#internal-access-policy)`,
    `${name}.csproj: effective AssemblyAttribute items must not generate InternalsVisibleTo; received 2 (see AGENTS.md#internal-access-policy)`,
  ]);
});

test('rejects exact friend declarations generated through legacy AssemblyAttributes inputs', async () => {
  const name = 'LegacyImportedFriends';
  const root = await createProtectedProject(name, {
    customProps: `<Project>
  <PropertyGroup>
    <AssemblyAttributesPath>GeneratedFriendAttributes.cs</AssemblyAttributesPath>
  </PropertyGroup>
  <ItemGroup>
    <Compile Remove="Properties/AssemblyInfo.cs" />
    <AssemblyAttributes Include="System.Runtime.CompilerServices.Internals&#86;isibleToAttribute">
      <_Parameter1>Puntiro.UnitTests</_Parameter1>
    </AssemblyAttributes>
    <AssemblyAttributes Include="System.Runtime.CompilerServices.Internals&#86;isibleToAttribute">
      <_Parameter1>Puntiro.IntegrationTests</_Parameter1>
    </AssemblyAttributes>
  </ItemGroup>
</Project>
`,
  });
  verifyMetadata(buildProject(root, name));

  const errors = await policyModule.inspectProtectedProjectInputs({
    dotnet,
    repositoryRoot: root,
    projectPath: `${name}.csproj`,
    canonicalSource: 'Properties/AssemblyInfo.cs',
    baseOutputPath: path.join(root, 'fresh-output'),
  });

  assert.deepEqual(errors, [
    `${name}.csproj: Properties/AssemblyInfo.cs must be an effective Compile item exactly once (received 0; see AGENTS.md#internal-access-policy)`,
    `${name}.csproj: effective AssemblyAttributes items must not generate InternalsVisibleTo; received 2 (see AGENTS.md#internal-access-policy)`,
  ]);
});

test('rejects a successful build that leaves only a stale fixed-path assembly', async () => {
  const name = 'StaleOutput';
  const root = await createProtectedProject(name);
  const staleAssembly = buildProject(root, name);
  await access(staleAssembly);
  await writeFile(path.join(root, 'Directory.Build.targets'), `<Project>
  <PropertyGroup>
    <BuildDependsOn></BuildDependsOn>
  </PropertyGroup>
</Project>
`);

  await assert.rejects(
    policyModule.buildFreshOutputs({
      dotnet,
      repositoryRoot: root,
      solutionPath: `${name}.csproj`,
      protectedAssemblyNames: [name],
      verifierAssemblyName: name,
    }),
    /fresh build must produce exactly one StaleOutput\.dll in its unique output root; received 0/,
  );
});
