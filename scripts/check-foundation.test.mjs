import assert from 'node:assert/strict';
import { mkdtemp, mkdir, rm, writeFile } from 'node:fs/promises';
import { test } from 'node:test';
import os from 'node:os';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { validateFoundation } from './check-foundation.mjs';

const projects = [
  'src/Puntiro.Contracts/Puntiro.Contracts.csproj',
  'src/Puntiro.Security/Puntiro.Security.csproj',
  'src/Puntiro.Modules.Identity/Puntiro.Modules.Identity.csproj',
  'src/Puntiro.Modules.Tenancy/Puntiro.Modules.Tenancy.csproj',
  'src/Puntiro.Modules.Integrations/Puntiro.Modules.Integrations.csproj',
  'apps/cloud/Puntiro.Cloud.csproj',
  'apps/agent/Puntiro.Agent.csproj',
  'apps/kiosk-shell/Puntiro.KioskShell.csproj',
  'tools/Puntiro.Provisioning/Puntiro.Provisioning.csproj',
  'tests/Puntiro.UnitTests/Puntiro.UnitTests.csproj',
  'tests/Puntiro.IntegrationTests/Puntiro.IntegrationTests.csproj'
];

const validFiles = {
  'Puntiro.slnx': `<Solution>\n${projects.map(project => `  <Project Path="${project}" />`).join('\n')}\n</Solution>\n`,
  'src/Puntiro.Contracts/Puntiro.Contracts.csproj': '<Project Sdk="Microsoft.NET.Sdk" />\n',
  'src/Puntiro.Contracts/ProtocolVersion.cs': 'namespace Puntiro.Contracts;\n',
  'src/Puntiro.Security/Puntiro.Security.csproj': '<Project Sdk="Microsoft.NET.Sdk" />\n',
  'src/Puntiro.Security/SecurityModuleMarker.cs': 'namespace Puntiro.Security;\n',
  'src/Puntiro.Security/Properties/AssemblyInfo.cs': 'using System.Runtime.CompilerServices;\n\n[assembly: InternalsVisibleTo("Puntiro.UnitTests")]\n[assembly: InternalsVisibleTo("Puntiro.IntegrationTests")]\n',
  'src/Puntiro.Modules.Identity/Puntiro.Modules.Identity.csproj': '<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><ProjectReference Include="../Puntiro.Security/Puntiro.Security.csproj" /></ItemGroup></Project>\n',
  'src/Puntiro.Modules.Identity/IdentityModuleMarker.cs': 'namespace Puntiro.Modules.Identity;\n',
  'src/Puntiro.Modules.Identity/Properties/AssemblyInfo.cs': 'using System.Runtime.CompilerServices;\n\n[assembly: InternalsVisibleTo("Puntiro.UnitTests")]\n[assembly: InternalsVisibleTo("Puntiro.IntegrationTests")]\n',
  'src/Puntiro.Modules.Tenancy/Puntiro.Modules.Tenancy.csproj': '<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><ProjectReference Include="../Puntiro.Security/Puntiro.Security.csproj" /></ItemGroup></Project>\n',
  'src/Puntiro.Modules.Tenancy/TenancyModuleMarker.cs': 'namespace Puntiro.Modules.Tenancy;\n',
  'src/Puntiro.Modules.Tenancy/Properties/AssemblyInfo.cs': 'using System.Runtime.CompilerServices;\n\n[assembly: InternalsVisibleTo("Puntiro.UnitTests")]\n[assembly: InternalsVisibleTo("Puntiro.IntegrationTests")]\n',
  'src/Puntiro.Modules.Integrations/Puntiro.Modules.Integrations.csproj': '<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><ProjectReference Include="../Puntiro.Security/Puntiro.Security.csproj" /></ItemGroup></Project>\n',
  'src/Puntiro.Modules.Integrations/IntegrationsModuleMarker.cs': 'namespace Puntiro.Modules.Integrations;\n',
  'src/Puntiro.Modules.Integrations/Properties/AssemblyInfo.cs': 'using System.Runtime.CompilerServices;\n\n[assembly: InternalsVisibleTo("Puntiro.UnitTests")]\n[assembly: InternalsVisibleTo("Puntiro.IntegrationTests")]\n',
  'apps/cloud/Puntiro.Cloud.csproj': '<Project Sdk="Microsoft.NET.Sdk.Web"><ItemGroup><ProjectReference Include="../../src/Puntiro.Contracts/Puntiro.Contracts.csproj" /><ProjectReference Include="../../src/Puntiro.Modules.Identity/Puntiro.Modules.Identity.csproj" /><ProjectReference Include="../../src/Puntiro.Modules.Tenancy/Puntiro.Modules.Tenancy.csproj" /><ProjectReference Include="../../src/Puntiro.Modules.Integrations/Puntiro.Modules.Integrations.csproj" /></ItemGroup></Project>\n',
  'apps/cloud/Program.cs': 'var app = WebApplication.CreateBuilder().Build();\n',
  'apps/agent/Puntiro.Agent.csproj': '<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><ProjectReference Include="../../src/Puntiro.Contracts/Puntiro.Contracts.csproj" /></ItemGroup></Project>\n',
  'apps/agent/Program.cs': 'Console.WriteLine("agent");\n',
  'apps/kiosk-shell/Puntiro.KioskShell.csproj': '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><UseWPF>true</UseWPF></PropertyGroup><ItemGroup><ProjectReference Include="../../src/Puntiro.Contracts/Puntiro.Contracts.csproj" /></ItemGroup></Project>\n',
  'apps/kiosk-shell/App.xaml': '<Application />\n',
  'tools/Puntiro.Provisioning/Puntiro.Provisioning.csproj': '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType></PropertyGroup><ItemGroup><ProjectReference Include="../../src/Puntiro.Modules.Identity/Puntiro.Modules.Identity.csproj" /><ProjectReference Include="../../src/Puntiro.Modules.Tenancy/Puntiro.Modules.Tenancy.csproj" /></ItemGroup></Project>\n',
  'tools/Puntiro.Provisioning/Program.cs': 'Console.WriteLine("provisioning");\n',
  'tools/Puntiro.Provisioning/Properties/AssemblyInfo.cs': 'using System.Runtime.CompilerServices;\n\n[assembly: InternalsVisibleTo("Puntiro.UnitTests")]\n[assembly: InternalsVisibleTo("Puntiro.IntegrationTests")]\n',
  'tests/Puntiro.UnitTests/Puntiro.UnitTests.csproj': '<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><ProjectReference Include="../../src/Puntiro.Security/Puntiro.Security.csproj" /><ProjectReference Include="../../src/Puntiro.Modules.Identity/Puntiro.Modules.Identity.csproj" /><ProjectReference Include="../../src/Puntiro.Modules.Tenancy/Puntiro.Modules.Tenancy.csproj" /><ProjectReference Include="../../src/Puntiro.Modules.Integrations/Puntiro.Modules.Integrations.csproj" /><ProjectReference Include="../../tools/Puntiro.Provisioning/Puntiro.Provisioning.csproj" /></ItemGroup></Project>\n',
  'tests/Puntiro.IntegrationTests/Puntiro.IntegrationTests.csproj': '<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><ProjectReference Include="../../apps/cloud/Puntiro.Cloud.csproj" /><ProjectReference Include="../../src/Puntiro.Modules.Identity/Puntiro.Modules.Identity.csproj" /><ProjectReference Include="../../src/Puntiro.Modules.Tenancy/Puntiro.Modules.Tenancy.csproj" /><ProjectReference Include="../../src/Puntiro.Modules.Integrations/Puntiro.Modules.Integrations.csproj" /><ProjectReference Include="../../tools/Puntiro.Provisioning/Puntiro.Provisioning.csproj" /></ItemGroup></Project>\n',
  'apps/admin/package.json': '{"private":true,"dependencies":{"@puntiro/ui":"workspace:*"}}\n',
  'apps/admin/src/App.tsx': '<main>Puntiro Admin</main>\n',
  'apps/kiosk-web/package.json': '{"private":true,"dependencies":{"@puntiro/ui":"workspace:*"}}\n',
  'apps/kiosk-web/src/App.tsx': '<main mode="touch">Puntiro Kiosk</main>\n'
};

async function createFoundationFixture(t) {
  const root = await mkdtemp(path.join(os.tmpdir(), 'puntiro-foundation-'));
  t.after(() => rm(root, { force: true, recursive: true }));

  await Promise.all(Object.entries(validFiles).map(async ([relativePath, content]) => {
    const target = path.join(root, relativePath);
    await mkdir(path.dirname(target), { recursive: true });
    await writeFile(target, content);
  }));

  return { root, rootUrl: pathToFileURL(`${root}${path.sep}`) };
}

test('product process boundaries are present and one-way', async () => {
  const errors = await validateFoundation(new URL('../', import.meta.url));
  assert.deepEqual(errors, []);
});

test('rejects a solution without every product project entry', async t => {
  const { root, rootUrl } = await createFoundationFixture(t);
  await writeFile(path.join(root, 'Puntiro.slnx'), '<Solution />\n');

  assert.deepEqual(await validateFoundation(rootUrl), projects.map(project =>
    `Puntiro.slnx must list project: ${project}`));
});

test('rejects a commented Contracts reference', async t => {
  const { root, rootUrl } = await createFoundationFixture(t);
  await writeFile(
    path.join(root, 'apps/cloud/Puntiro.Cloud.csproj'),
    '<Project Sdk="Microsoft.NET.Sdk.Web"><ItemGroup><!-- <ProjectReference Include="../../src/Puntiro.Contracts/Puntiro.Contracts.csproj" /> --><ProjectReference Include="../../src/Puntiro.Modules.Identity/Puntiro.Modules.Identity.csproj" /><ProjectReference Include="../../src/Puntiro.Modules.Tenancy/Puntiro.Modules.Tenancy.csproj" /><ProjectReference Include="../../src/Puntiro.Modules.Integrations/Puntiro.Modules.Integrations.csproj" /></ItemGroup></Project>\n'
  );

  assert.deepEqual(await validateFoundation(rootUrl), [
    'apps/cloud/Puntiro.Cloud.csproj must reference Puntiro.Contracts'
  ]);
});

test('rejects Contracts referencing an application project', async t => {
  const { root, rootUrl } = await createFoundationFixture(t);
  await writeFile(
    path.join(root, 'src/Puntiro.Contracts/Puntiro.Contracts.csproj'),
    '<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><ProjectReference Include="../../apps/cloud/Puntiro.Cloud.csproj" /></ItemGroup></Project>\n'
  );

  assert.deepEqual(await validateFoundation(rootUrl), [
    'Puntiro.Contracts must not reference application project: apps/cloud/Puntiro.Cloud.csproj'
  ]);
});

test('rejects application projects referencing another application', async t => {
  const { root, rootUrl } = await createFoundationFixture(t);
  await writeFile(
    path.join(root, 'apps/agent/Puntiro.Agent.csproj'),
    `<Project Sdk="Microsoft.NET.Sdk"><ItemGroup>
      <ProjectReference Include="../../src/Puntiro.Contracts/Puntiro.Contracts.csproj" />
      <ProjectReference Include="../cloud/Puntiro.Cloud.csproj" />
    </ItemGroup></Project>\n`
  );

  assert.deepEqual(await validateFoundation(rootUrl), [
    'apps/agent/Puntiro.Agent.csproj must not reference application project: apps/cloud/Puntiro.Cloud.csproj'
  ]);
});

test('cloud may compose approved modules but modules cannot reference each other', async t => {
  const { root, rootUrl } = await createFoundationFixture(t);
  await writeFile(
    path.join(root, 'src/Puntiro.Modules.Identity/Puntiro.Modules.Identity.csproj'),
    '<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><ProjectReference Include="../Puntiro.Security/Puntiro.Security.csproj" /><ProjectReference Include="../Puntiro.Modules.Tenancy/Puntiro.Modules.Tenancy.csproj" /></ItemGroup></Project>',
  );

  assert.deepEqual(await validateFoundation(rootUrl), [
    'src/Puntiro.Modules.Identity/Puntiro.Modules.Identity.csproj must not reference module: src/Puntiro.Modules.Tenancy/Puntiro.Modules.Tenancy.csproj',
  ]);
});

test('modules must reference Puntiro.Security', async t => {
  const { root, rootUrl } = await createFoundationFixture(t);
  await writeFile(
    path.join(root, 'src/Puntiro.Modules.Identity/Puntiro.Modules.Identity.csproj'),
    '<Project Sdk="Microsoft.NET.Sdk" />',
  );

  assert.deepEqual(await validateFoundation(rootUrl), [
    'src/Puntiro.Modules.Identity/Puntiro.Modules.Identity.csproj must reference required security project: src/Puntiro.Security/Puntiro.Security.csproj',
  ]);
});

test('Provisioning must reference both required modules', async t => {
  const { root, rootUrl } = await createFoundationFixture(t);
  await writeFile(
    path.join(root, 'tools/Puntiro.Provisioning/Puntiro.Provisioning.csproj'),
    '<Project Sdk="Microsoft.NET.Sdk" />',
  );

  assert.deepEqual(await validateFoundation(rootUrl), [
    'tools/Puntiro.Provisioning/Puntiro.Provisioning.csproj must reference required module: src/Puntiro.Modules.Identity/Puntiro.Modules.Identity.csproj',
    'tools/Puntiro.Provisioning/Puntiro.Provisioning.csproj must reference required module: src/Puntiro.Modules.Tenancy/Puntiro.Modules.Tenancy.csproj',
  ]);
});

test('test projects must reference their approved project graphs', async t => {
  const { root, rootUrl } = await createFoundationFixture(t);
  await writeFile(path.join(root, 'tests/Puntiro.UnitTests/Puntiro.UnitTests.csproj'), '<Project Sdk="Microsoft.NET.Sdk" />');
  await writeFile(path.join(root, 'tests/Puntiro.IntegrationTests/Puntiro.IntegrationTests.csproj'), '<Project Sdk="Microsoft.NET.Sdk" />');

  assert.deepEqual(await validateFoundation(rootUrl), [
    'tests/Puntiro.UnitTests/Puntiro.UnitTests.csproj must reference required project: src/Puntiro.Security/Puntiro.Security.csproj',
    'tests/Puntiro.UnitTests/Puntiro.UnitTests.csproj must reference required project: src/Puntiro.Modules.Identity/Puntiro.Modules.Identity.csproj',
    'tests/Puntiro.UnitTests/Puntiro.UnitTests.csproj must reference required project: src/Puntiro.Modules.Tenancy/Puntiro.Modules.Tenancy.csproj',
    'tests/Puntiro.UnitTests/Puntiro.UnitTests.csproj must reference required project: src/Puntiro.Modules.Integrations/Puntiro.Modules.Integrations.csproj',
    'tests/Puntiro.UnitTests/Puntiro.UnitTests.csproj must reference required project: tools/Puntiro.Provisioning/Puntiro.Provisioning.csproj',
    'tests/Puntiro.IntegrationTests/Puntiro.IntegrationTests.csproj must reference required project: apps/cloud/Puntiro.Cloud.csproj',
    'tests/Puntiro.IntegrationTests/Puntiro.IntegrationTests.csproj must reference required project: src/Puntiro.Modules.Identity/Puntiro.Modules.Identity.csproj',
    'tests/Puntiro.IntegrationTests/Puntiro.IntegrationTests.csproj must reference required project: src/Puntiro.Modules.Tenancy/Puntiro.Modules.Tenancy.csproj',
    'tests/Puntiro.IntegrationTests/Puntiro.IntegrationTests.csproj must reference required project: src/Puntiro.Modules.Integrations/Puntiro.Modules.Integrations.csproj',
    'tests/Puntiro.IntegrationTests/Puntiro.IntegrationTests.csproj must reference required project: tools/Puntiro.Provisioning/Puntiro.Provisioning.csproj',
  ]);
});

test('test projects must not reference projects outside their approved graphs', async t => {
  const { root, rootUrl } = await createFoundationFixture(t);
  await writeFile(
    path.join(root, 'tests/Puntiro.UnitTests/Puntiro.UnitTests.csproj'),
    '<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><ProjectReference Include="../../src/Puntiro.Security/Puntiro.Security.csproj" /><ProjectReference Include="../../src/Puntiro.Modules.Identity/Puntiro.Modules.Identity.csproj" /><ProjectReference Include="../../src/Puntiro.Modules.Tenancy/Puntiro.Modules.Tenancy.csproj" /><ProjectReference Include="../../src/Puntiro.Modules.Integrations/Puntiro.Modules.Integrations.csproj" /><ProjectReference Include="../../tools/Puntiro.Provisioning/Puntiro.Provisioning.csproj" /><ProjectReference Include="../../src/Puntiro.Contracts/Puntiro.Contracts.csproj" /></ItemGroup></Project>',
  );

  assert.deepEqual(await validateFoundation(rootUrl), [
    'tests/Puntiro.UnitTests/Puntiro.UnitTests.csproj must not reference project: src/Puntiro.Contracts/Puntiro.Contracts.csproj',
  ]);
});

test('production internals are visible only to the named test assemblies', async t => {
  const { root, rootUrl } = await createFoundationFixture(t);
  await writeFile(
    path.join(root, 'src/Puntiro.Modules.Identity/Properties/AssemblyInfo.cs'),
    'using System.Runtime.CompilerServices;\n[assembly: InternalsVisibleTo("Puntiro.UnitTests")]\n[assembly: InternalsVisibleTo("Unapproved.Tests")]\n',
  );

  assert.deepEqual(await validateFoundation(rootUrl), [
    'src/Puntiro.Modules.Identity/Properties/AssemblyInfo.cs must contain exactly the canonical InternalsVisibleTo declarations',
  ]);
});

test('production internals are enforced across every source file', async t => {
  const { root, rootUrl } = await createFoundationFixture(t);
  await writeFile(
    path.join(root, 'src/Puntiro.Modules.Identity/AlternateAssemblyInfo.cs'),
    '[assembly: global::System.Runtime.CompilerServices.InternalsVisibleTo(@"Unapproved.Tests")]\n',
  );

  assert.deepEqual(await validateFoundation(rootUrl), [
    'src/Puntiro.Modules.Identity/AlternateAssemblyInfo.cs must not contain InternalsVisibleTo outside src/Puntiro.Modules.Identity/Properties/AssemblyInfo.cs',
  ]);
});

test('accepts the exact canonical InternalsVisibleTo declaration file', async t => {
  const { root, rootUrl } = await createFoundationFixture(t);
  await writeFile(
    path.join(root, 'src/Puntiro.Modules.Identity/Properties/AssemblyInfo.cs'),
    'using System.Runtime.CompilerServices;\n\n[assembly: InternalsVisibleTo("Puntiro.UnitTests")]\n[assembly: InternalsVisibleTo("Puntiro.IntegrationTests")]\n',
  );

  assert.deepEqual(await validateFoundation(rootUrl), []);
});

test('rejects dynamic assembly InternalsVisibleTo declarations', async t => {
  const { root, rootUrl } = await createFoundationFixture(t);
  await writeFile(
    path.join(root, 'src/Puntiro.Modules.Identity/DynamicAssemblyInfo.cs'),
    '[assembly: InternalsVisibleTo(GetTestAssemblyName())]\n',
  );

  assert.deepEqual(await validateFoundation(rootUrl), [
    'src/Puntiro.Modules.Identity/DynamicAssemblyInfo.cs must not contain InternalsVisibleTo outside src/Puntiro.Modules.Identity/Properties/AssemblyInfo.cs',
  ]);
});

test('rejects InternalsVisibleTo declarations in generated source directories', async t => {
  const { root, rootUrl } = await createFoundationFixture(t);
  const sourcePath = path.join(root, 'src/Puntiro.Modules.Identity/generated/GeneratedAssemblyInfo.cs');
  await mkdir(path.dirname(sourcePath), { recursive: true });
  await writeFile(sourcePath, '[assembly: InternalsVisibleTo("Unapproved.Tests")]\n');

  assert.deepEqual(await validateFoundation(rootUrl), [
    'src/Puntiro.Modules.Identity/generated/GeneratedAssemblyInfo.cs must not contain InternalsVisibleTo outside src/Puntiro.Modules.Identity/Properties/AssemblyInfo.cs',
  ]);
});

test('rejects aliased InternalsVisibleTo declarations outside the canonical file', async t => {
  const { root, rootUrl } = await createFoundationFixture(t);
  await writeFile(
    path.join(root, 'src/Puntiro.Modules.Identity/AliasedAssemblyInfo.cs'),
    'using IVT = System.Runtime.CompilerServices.InternalsVisibleToAttribute;\n[assembly: IVT("Unapproved.Tests")]\n',
  );

  assert.deepEqual(await validateFoundation(rootUrl), [
    'src/Puntiro.Modules.Identity/AliasedAssemblyInfo.cs must not contain InternalsVisibleTo outside src/Puntiro.Modules.Identity/Properties/AssemblyInfo.cs',
  ]);
});

test('reserves InternalsVisibleTo outside the canonical file even in ordinary strings', async t => {
  const { root, rootUrl } = await createFoundationFixture(t);
  await writeFile(
    path.join(root, 'src/Puntiro.Modules.Identity/ReservedToken.cs'),
    'const string policy = "InternalsVisibleTo";\n',
  );

  assert.deepEqual(await validateFoundation(rootUrl), [
    'src/Puntiro.Modules.Identity/ReservedToken.cs must not contain InternalsVisibleTo outside src/Puntiro.Modules.Identity/Properties/AssemblyInfo.cs',
  ]);
});

test('rejects MSBuild InternalsVisibleTo configuration in a project file', async t => {
  const { root, rootUrl } = await createFoundationFixture(t);
  await writeFile(
    path.join(root, 'src/Puntiro.Modules.Identity/Puntiro.Modules.Identity.csproj'),
    '<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><ProjectReference Include="../Puntiro.Security/Puntiro.Security.csproj" /><AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleToAttribute"><_Parameter1>Unapproved.Tests</_Parameter1></AssemblyAttribute></ItemGroup></Project>',
  );

  assert.deepEqual(await validateFoundation(rootUrl), [
    'src/Puntiro.Modules.Identity/Puntiro.Modules.Identity.csproj must not configure InternalsVisibleTo through MSBuild',
  ]);
});

test('rejects inherited MSBuild InternalsVisibleTo configuration', async t => {
  const { root, rootUrl } = await createFoundationFixture(t);
  await writeFile(
    path.join(root, 'Directory.Build.props'),
    '<Project><ItemGroup><AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleToAttribute" /></ItemGroup></Project>',
  );

  assert.deepEqual(await validateFoundation(rootUrl), [
    'Directory.Build.props must not configure InternalsVisibleTo through MSBuild',
  ]);
});

test('rejects deferred APIs in project source files', async t => {
  const { root, rootUrl } = await createFoundationFixture(t);
  await writeFile(path.join(root, 'apps/cloud/Program.cs'), 'using Microsoft.Data.Sqlite;\n');

  assert.deepEqual(await validateFoundation(rootUrl), [
    'apps/cloud/Program.cs contains deferred dependency Microsoft.Data.Sqlite'
  ]);
});

test('rejects forbidden UI boundary code outside App.tsx', async t => {
  const { root, rootUrl } = await createFoundationFixture(t);
  const sourcePath = path.join(root, 'apps/admin/src/transport/client.ts');
  await mkdir(path.dirname(sourcePath), { recursive: true });
  await writeFile(sourcePath, 'export const socket = new WebSocket("ws://device");\n');

  assert.deepEqual(await validateFoundation(rootUrl), [
    'apps/admin/src/transport/client.ts contains forbidden boundary marker WebSocket'
  ]);
});

test('rejects forbidden UI dependencies in package manifests', async t => {
  const { root, rootUrl } = await createFoundationFixture(t);
  await writeFile(
    path.join(root, 'apps/kiosk-web/package.json'),
    '{"private":true,"dependencies":{"@puntiro/ui":"workspace:*","better-sqlite3":"1.2.3"}}\n'
  );

  assert.deepEqual(await validateFoundation(rootUrl), [
    'apps/kiosk-web/package.json contains forbidden boundary marker sqlite'
  ]);
});

test('ignores generated UI outputs while scanning boundaries', async t => {
  const { root, rootUrl } = await createFoundationFixture(t);
  const generatedPath = path.join(root, 'apps/admin/dist/generated.js');
  await mkdir(path.dirname(generatedPath), { recursive: true });
  await writeFile(generatedPath, 'new WebSocket("ws://generated");\n');

  assert.deepEqual(await validateFoundation(rootUrl), []);
});
