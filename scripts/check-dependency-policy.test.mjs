import assert from 'node:assert/strict';
import { mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { pathToFileURL } from 'node:url';
import { validateDependencyPolicy } from './check-dependency-policy.mjs';

async function withPolicyFixture(options, run) {
  const root = await mkdtemp(path.join(tmpdir(), 'puntiro-dependency-policy-'));
  try {
    await mkdir(path.join(root, 'apps'));
    await mkdir(path.join(root, 'packages'));
    await writeFile(path.join(root, 'package.json'), JSON.stringify({
      name: 'policy-fixture',
      packageManager: 'pnpm@11.17.0',
      engines: { node: '24.19.0' },
      ...options.rootManifest,
    }));
    await writeFile(path.join(root, 'global.json'), JSON.stringify({
      sdk: {
        version: '10.0.302',
        rollForward: 'disable',
        allowPrerelease: false,
      },
    }));
    await writeFile(
      path.join(root, 'Directory.Packages.props'),
      options.centralPackages ?? '<Project />',
    );

    for (const workspace of options.workspaces ?? []) {
      const directory = path.join(root, workspace.directory);
      await mkdir(directory, { recursive: true });
      await writeFile(path.join(directory, 'package.json'), JSON.stringify(workspace.manifest));
    }

    await run(pathToFileURL(`${root}${path.sep}`));
  } finally {
    await rm(root, { recursive: true, force: true });
  }
}

test('toolchains and package manifests use exact stable versions', async () => {
  const errors = await validateDependencyPolicy(new URL('../', import.meta.url));
  assert.deepEqual(errors, []);
});

test('root Node engine is pinned exactly', async () => {
  await withPolicyFixture({
    rootManifest: { engines: { node: '>=24 <25' } },
  }, async (rootUrl) => {
    assert.deepEqual(await validateDependencyPolicy(rootUrl), [
      'engines.node must be 24.19.0, received >=24 <25',
    ]);
  });
});

test('workspace protocol is limited to first-party Puntiro workspace packages', async () => {
  await withPolicyFixture({
    rootManifest: {
      dependencies: {
        '@puntiro/ui': 'workspace:*',
        external: 'workspace:*',
      },
    },
    workspaces: [{
      directory: 'packages/ui',
      manifest: { name: '@puntiro/ui', version: '0.1.0' },
    }],
  }, async (rootUrl) => {
    assert.deepEqual(await validateDependencyPolicy(rootUrl), [
      'package.json dependencies.external cannot use workspace:*',
    ]);
  });
});

test('NuGet policy validates alternate declarations and rejects missing or non-exact versions', async () => {
  await withPolicyFixture({
    centralPackages: `<Project>
      <ItemGroup>
        <PackageVersion Version='1.2.3' Include='AttributeOrder' />
        <PackageVersion Update="Multiline"
                        Version="2.3.4" />
        <PackageVersion Include="ChildVersion"><Version>3.4.5</Version></PackageVersion>
        <PackageVersion Include="MissingVersion" />
        <PackageVersion Update='RangeVersion'><Version>[1.0.0,2.0.0)</Version></PackageVersion>
      </ItemGroup>
    </Project>`,
  }, async (rootUrl) => {
    assert.deepEqual(await validateDependencyPolicy(rootUrl), [
      'NuGet package MissingVersion is missing an exact version',
      'NuGet package RangeVersion is not exact: [1.0.0,2.0.0)',
    ]);
  });
});

test('planned CI assertions use approved Node and .NET pins', async () => {
  const plan = await readFile(
    new URL('../docs/superpowers/plans/2026-08-08-puntiro-repository-foundation.md', import.meta.url),
    'utf8',
  );

  assert.doesNotMatch(plan, /assert\.match\(workflow, \/node-version: 24\\\.18\\\.0\//);
  assert.doesNotMatch(plan, /assert\.match\(workflow, \/dotnet-version: 10\\\.0\\\.102\//);
  assert.match(plan, /assert\.match\(workflow, \/node-version: 24\\\.19\\\.0\//);
  assert.match(plan, /assert\.match\(workflow, \/dotnet-version: 10\\\.0\\\.302\//);
});
