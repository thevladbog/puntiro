import assert from 'node:assert/strict';
import { copyFile, mkdtemp, mkdir, rm, writeFile } from 'node:fs/promises';
import { spawnSync } from 'node:child_process';
import { after, before, test } from 'node:test';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repositoryRoot = fileURLToPath(new URL('../', import.meta.url));
const dotnet = process.env.PUNTIRO_DOTNET_BIN ?? 'dotnet';
const approvedAssemblyInfo = `using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Puntiro.UnitTests")]
[assembly: InternalsVisibleTo("Puntiro.IntegrationTests")]
`;

let testRoot;
let verifierDll;
const scratchAssemblies = new Map();

function runDotnet(args, cwd) {
  return spawnSync(dotnet, args, {
    cwd,
    encoding: 'utf8',
    timeout: 180_000,
    env: {
      ...process.env,
      DOTNET_CLI_TELEMETRY_OPTOUT: '1',
      DOTNET_NOLOGO: '1',
    },
  });
}

function commandFailure(result) {
  return [result.error?.message, result.stdout, result.stderr].filter(Boolean).join('\n');
}

before(async () => {
  testRoot = await mkdtemp(path.join(os.tmpdir(), 'puntiro-internals-metadata-'));
  const toolOutput = path.join(testRoot, 'verifier');
  const toolArtifacts = path.join(testRoot, 'verifier-artifacts');
  const build = runDotnet([
    'build',
    path.join(repositoryRoot, 'tools/Puntiro.AssemblyPolicy/Puntiro.AssemblyPolicy.csproj'),
    '--configuration', 'Release',
    '--output', toolOutput,
    '--artifacts-path', toolArtifacts,
    '-p:RestoreLockedMode=true',
  ], repositoryRoot);

  assert.equal(build.status, 0, `failed to build assembly-policy verifier:\n${commandFailure(build)}`);
  verifierDll = path.join(toolOutput, 'Puntiro.AssemblyPolicy.dll');

  const scratchAssembly = await prepareScratchAssembly('scratch');
  const scratchRoot = path.join(testRoot, 'scratch');
  const approvedBuild = runDotnet(['build', 'scratch.csproj', '--configuration', 'Release'], scratchRoot);
  assert.equal(
    approvedBuild.status,
    0,
    `approved scratch assembly must compile successfully:\n${commandFailure(approvedBuild)}`,
  );
  const approvedAssembly = path.join(testRoot, 'approved.dll');
  await copyFile(scratchAssembly, approvedAssembly);
  scratchAssemblies.set('approved', approvedAssembly);

  await prepareScratchAssembly('scratch', {
    projectExtra: `<ItemGroup>
    <AssemblyAttribute Include="System.Runtime.CompilerServices.Internals&#86;isibleToAttribute">
      <_Parameter1>Xml.Entity.Tests</_Parameter1>
    </AssemblyAttribute>
  </ItemGroup>
  <Import Project="Custom.props" />`,
    files: {
      'Custom.props': `<Project>
  <ItemGroup>
    <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleToAttribute">
      <_Parameter1>Imported.Props.Tests</_Parameter1>
    </AssemblyAttribute>
  </ItemGroup>
</Project>
`,
      'EscapedAssemblyInfo.cs': String.raw`[assembly: System.Runtime.CompilerServices.Internals\u0056isibleToAttribute("Unicode.Escape.Tests")]
`,
    },
  });
  const bypassBuild = runDotnet(['build', 'scratch.csproj', '--configuration', 'Release'], scratchRoot);
  assert.equal(
    bypassBuild.status,
    0,
    `all scratch bypasses must compile successfully:\n${commandFailure(bypassBuild)}`,
  );
  const bypassAssembly = path.join(testRoot, 'bypasses.dll');
  await copyFile(scratchAssembly, bypassAssembly);
  scratchAssemblies.set('imported-props', bypassAssembly);
  scratchAssemblies.set('xml-entity', bypassAssembly);
  scratchAssemblies.set('unicode-escape', bypassAssembly);

  const missingAssembly = await prepareScratchAssembly('missing', {
    assemblyInfo: `using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Puntiro.UnitTests")]
`,
  });
  const missingBuild = runDotnet(
    ['build', 'missing.csproj', '--configuration', 'Release'],
    path.join(testRoot, 'missing'),
  );
  assert.equal(missingBuild.status, 0, `missing-friend scratch assembly must compile:\n${commandFailure(missingBuild)}`);
  scratchAssemblies.set('missing', missingAssembly);

  const fakeAssembly = await prepareScratchAssembly('fake', {
    assemblyInfo: `[assembly: System.Runtime.CompilerServices.InternalsVisibleToAttribute("Puntiro.UnitTests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleToAttribute("Puntiro.IntegrationTests")]
`,
    files: {
      'FakeFriendAttribute.cs': `namespace System.Runtime.CompilerServices;

[global::System.AttributeUsage(global::System.AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class InternalsVisibleToAttribute(string assemblyName) : global::System.Attribute
{
    public string AssemblyName { get; } = assemblyName;
}
`,
    },
  });
  const fakeBuild = runDotnet(
    ['build', 'fake.csproj', '--configuration', 'Release'],
    path.join(testRoot, 'fake'),
  );
  assert.equal(fakeBuild.status, 0, `fake-friend scratch assembly must compile:\n${commandFailure(fakeBuild)}`);
  scratchAssemblies.set('fake', fakeAssembly);

  const emitterRoot = path.join(testRoot, 'emitter');
  await mkdir(emitterRoot, { recursive: true });
  await writeFile(path.join(emitterRoot, 'global.json'), JSON.stringify({
    sdk: {
      version: '10.0.302',
      rollForward: 'disable',
      allowPrerelease: false,
    },
  }));
  await writeFile(path.join(emitterRoot, 'emitter.csproj'), `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
</Project>
`);
  await writeFile(path.join(emitterRoot, 'Program.cs'), `using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

var assembly = new PersistedAssemblyBuilder(new AssemblyName("duplicate"), typeof(object).Assembly);
var module = assembly.DefineDynamicModule("duplicate");
var constructor = typeof(InternalsVisibleToAttribute).GetConstructor([typeof(string)])!;
foreach (var friend in new[] { "Puntiro.UnitTests", "Puntiro.UnitTests", "Puntiro.IntegrationTests" })
{
    assembly.SetCustomAttribute(new CustomAttributeBuilder(constructor, [friend]));
}
module.DefineType("Marker", TypeAttributes.Public).CreateType();
assembly.Save(args[0]);
`);
  const duplicateAssembly = path.join(testRoot, 'duplicate.dll');
  const duplicateBuild = runDotnet(
    ['run', '--project', 'emitter.csproj', '--configuration', 'Release', '--', duplicateAssembly],
    emitterRoot,
  );
  assert.equal(
    duplicateBuild.status,
    0,
    `duplicate-friend metadata emitter must succeed:\n${commandFailure(duplicateBuild)}`,
  );
  scratchAssemblies.set('duplicate', duplicateAssembly);
});

after(async () => {
  if (testRoot) await rm(testRoot, { force: true, recursive: true });
});

async function prepareScratchAssembly(name, {
  projectExtra = '',
  files = {},
  assemblyInfo = approvedAssemblyInfo,
} = {}) {
  const root = path.join(testRoot, name);
  await mkdir(path.join(root, 'Properties'), { recursive: true });
  await writeFile(path.join(root, 'global.json'), JSON.stringify({
    sdk: {
      version: '10.0.302',
      rollForward: 'disable',
      allowPrerelease: false,
    },
  }));
  await writeFile(path.join(root, `${name}.csproj`), `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  ${projectExtra}
</Project>
`);
  await writeFile(path.join(root, 'Properties/AssemblyInfo.cs'), assemblyInfo);
  await writeFile(path.join(root, 'Marker.cs'), 'public static class Marker;\n');

  for (const [relativePath, content] of Object.entries(files)) {
    const target = path.join(root, relativePath);
    await mkdir(path.dirname(target), { recursive: true });
    await writeFile(target, content);
  }

  return path.join(root, `bin/Release/net10.0/${name}.dll`);
}

function verifyAssembly(assemblyPath, expectedAssemblyName = 'scratch') {
  return runDotnet([verifierDll, expectedAssemblyName, assemblyPath], repositoryRoot);
}

function assertRejectedFriend(result, friendName) {
  assert.equal(result.status, 1, commandFailure(result));
  assert.ok(result.stderr.includes(friendName), result.stderr);
  assert.match(result.stderr, /AGENTS\.md#internal-access-policy/);
}

test('accepts an assembly with exactly the two approved friends', async () => {
  const result = verifyAssembly(scratchAssemblies.get('approved'));

  assert.equal(result.status, 0, commandFailure(result));
});

test('rejects a collision DLL whose assembly identity does not match the protected project', async () => {
  const result = verifyAssembly(scratchAssemblies.get('approved'), 'Protected.Project');

  assert.equal(result.status, 1, commandFailure(result));
  assert.match(result.stderr, /expected Protected\.Project; received scratch/);
  assert.match(result.stderr, /AGENTS\.md#internal-access-policy/);
});

test('rejects duplicate protected assembly inputs', async () => {
  const assemblyPath = scratchAssemblies.get('approved');
  const result = runDotnet([
    verifierDll,
    'scratch', assemblyPath,
    'scratch', assemblyPath,
  ], repositoryRoot);

  assert.equal(result.status, 2, commandFailure(result));
  assert.match(result.stderr, /duplicate expected assembly identity: scratch/);
});

test('rejects a protected assembly with a missing approved friend', async () => {
  const result = verifyAssembly(scratchAssemblies.get('missing'), 'missing');

  assert.equal(result.status, 1, commandFailure(result));
  assert.match(result.stderr, /received \[Puntiro\.UnitTests\]/);
});

test('rejects duplicate approved friend metadata', async () => {
  const result = verifyAssembly(scratchAssemblies.get('duplicate'), 'duplicate');

  assert.equal(result.status, 1, commandFailure(result));
  assert.match(result.stderr, /Puntiro\.UnitTests, Puntiro\.UnitTests/);
});

test('rejects same-named friend attributes that are not the BCL attribute', async () => {
  const result = verifyAssembly(scratchAssemblies.get('fake'), 'fake');

  assert.equal(result.status, 1, commandFailure(result));
  assert.match(result.stderr, /received \[\]/);
});

test('rejects an unapproved friend injected through an arbitrary imported props file', async () => {
  assertRejectedFriend(verifyAssembly(scratchAssemblies.get('imported-props')), 'Imported.Props.Tests');
});

test('rejects an unapproved friend hidden behind an XML entity in MSBuild', async () => {
  assertRejectedFriend(verifyAssembly(scratchAssemblies.get('xml-entity')), 'Xml.Entity.Tests');
});

test('rejects an unapproved friend hidden behind a C# Unicode escape', async () => {
  assertRejectedFriend(verifyAssembly(scratchAssemblies.get('unicode-escape')), 'Unicode.Escape.Tests');
});
