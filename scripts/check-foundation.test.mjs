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
  'src/Puntiro.Modules.Identity/Puntiro.Modules.Identity.csproj': '<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><ProjectReference Include="../Puntiro.Security/Puntiro.Security.csproj" /></ItemGroup></Project>\n',
  'src/Puntiro.Modules.Identity/IdentityModuleMarker.cs': 'namespace Puntiro.Modules.Identity;\n',
  'src/Puntiro.Modules.Tenancy/Puntiro.Modules.Tenancy.csproj': '<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><ProjectReference Include="../Puntiro.Security/Puntiro.Security.csproj" /></ItemGroup></Project>\n',
  'src/Puntiro.Modules.Tenancy/TenancyModuleMarker.cs': 'namespace Puntiro.Modules.Tenancy;\n',
  'src/Puntiro.Modules.Integrations/Puntiro.Modules.Integrations.csproj': '<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><ProjectReference Include="../Puntiro.Security/Puntiro.Security.csproj" /></ItemGroup></Project>\n',
  'src/Puntiro.Modules.Integrations/IntegrationsModuleMarker.cs': 'namespace Puntiro.Modules.Integrations;\n',
  'apps/cloud/Puntiro.Cloud.csproj': '<Project Sdk="Microsoft.NET.Sdk.Web"><ItemGroup><ProjectReference Include="../../src/Puntiro.Contracts/Puntiro.Contracts.csproj" /><ProjectReference Include="../../src/Puntiro.Modules.Identity/Puntiro.Modules.Identity.csproj" /><ProjectReference Include="../../src/Puntiro.Modules.Tenancy/Puntiro.Modules.Tenancy.csproj" /><ProjectReference Include="../../src/Puntiro.Modules.Integrations/Puntiro.Modules.Integrations.csproj" /></ItemGroup></Project>\n',
  'apps/cloud/Program.cs': 'var app = WebApplication.CreateBuilder().Build();\n',
  'apps/agent/Puntiro.Agent.csproj': '<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><ProjectReference Include="../../src/Puntiro.Contracts/Puntiro.Contracts.csproj" /></ItemGroup></Project>\n',
  'apps/agent/Program.cs': 'Console.WriteLine("agent");\n',
  'apps/kiosk-shell/Puntiro.KioskShell.csproj': '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><UseWPF>true</UseWPF></PropertyGroup><ItemGroup><ProjectReference Include="../../src/Puntiro.Contracts/Puntiro.Contracts.csproj" /></ItemGroup></Project>\n',
  'apps/kiosk-shell/App.xaml': '<Application />\n',
  'tools/Puntiro.Provisioning/Puntiro.Provisioning.csproj': '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType></PropertyGroup><ItemGroup><ProjectReference Include="../../src/Puntiro.Modules.Identity/Puntiro.Modules.Identity.csproj" /><ProjectReference Include="../../src/Puntiro.Modules.Tenancy/Puntiro.Modules.Tenancy.csproj" /></ItemGroup></Project>\n',
  'tools/Puntiro.Provisioning/Program.cs': 'Console.WriteLine("provisioning");\n',
  'tests/Puntiro.UnitTests/Puntiro.UnitTests.csproj': '<Project Sdk="Microsoft.NET.Sdk" />\n',
  'tests/Puntiro.IntegrationTests/Puntiro.IntegrationTests.csproj': '<Project Sdk="Microsoft.NET.Sdk" />\n',
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
    '<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><ProjectReference Include="../Puntiro.Modules.Tenancy/Puntiro.Modules.Tenancy.csproj" /></ItemGroup></Project>',
  );

  assert.deepEqual(await validateFoundation(rootUrl), [
    'src/Puntiro.Modules.Identity/Puntiro.Modules.Identity.csproj must not reference module: src/Puntiro.Modules.Tenancy/Puntiro.Modules.Tenancy.csproj',
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
