import assert from 'node:assert/strict';
import { mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { pathToFileURL } from 'node:url';
import { validateDependencyPolicy } from './check-dependency-policy.mjs';

const validCentralPackages = `<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageVersionOverrideEnabled>false</CentralPackageVersionOverrideEnabled>
  </PropertyGroup>
</Project>`;
const validDotnetTools = {
  version: 1,
  isRoot: true,
  tools: {
    'dotnet-ef': {
      version: '10.0.10',
      commands: ['dotnet-ef'],
    },
  },
};

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
      options.centralPackages ?? validCentralPackages,
    );
    await mkdir(path.join(root, '.config'));
    await writeFile(
      path.join(root, '.config', 'dotnet-tools.json'),
      JSON.stringify(options.dotnetTools ?? validDotnetTools),
    );

    for (const workspace of options.workspaces ?? []) {
      const directory = path.join(root, workspace.directory);
      await mkdir(directory, { recursive: true });
      await writeFile(path.join(directory, 'package.json'), JSON.stringify(workspace.manifest));
    }

    for (const [relativePath, content] of Object.entries(options.nugetFiles ?? {})) {
      const target = path.join(root, relativePath);
      await mkdir(path.dirname(target), { recursive: true });
      await writeFile(target, content);
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

test('repository dotnet tools use exact approved versions', async () => {
  const manifest = JSON.parse(await readFile(new URL('../.config/dotnet-tools.json', import.meta.url), 'utf8'));
  assert.equal(manifest.tools['dotnet-ef'].version, '10.0.10');
  assert.deepEqual(manifest.tools['dotnet-ef'].commands, ['dotnet-ef']);
});

test('dependency policy rejects an unapproved dotnet-ef version', async () => {
  await withPolicyFixture({
    dotnetTools: {
      ...validDotnetTools,
      tools: {
        'dotnet-ef': {
          version: '10.0.9',
          commands: ['dotnet-ef'],
        },
      },
    },
  }, async (rootUrl) => {
    assert.deepEqual(await validateDependencyPolicy(rootUrl), [
      '.config/dotnet-tools.json tools.dotnet-ef.version must be 10.0.10, received 10.0.9',
    ]);
  });
});

test('npm registry and lockfile are portable to GitHub runners', async () => {
  const npmrc = await readFile(new URL('../.npmrc', import.meta.url), 'utf8').catch(() => '');
  const lockfile = await readFile(new URL('../pnpm-lock.yaml', import.meta.url), 'utf8');

  assert.match(npmrc, /^registry=https:\/\/registry\.npmjs\.org\/$/m);
  assert.doesNotMatch(lockfile, /npm\.yandex-team\.ru/);
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

test('development bootstrap requires the exact root Node engine', async () => {
  const rootManifest = JSON.parse(await readFile(new URL('../package.json', import.meta.url), 'utf8'));
  const runbook = await readFile(
    new URL('../docs/runbooks/development-bootstrap.md', import.meta.url),
    'utf8',
  );
  const escapedEngine = rootManifest.engines.node.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

  assert.match(runbook, new RegExp(`^- Node\\.js ${escapedEngine}$`, 'm'));
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
      <PropertyGroup>
        <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
        <CentralPackageVersionOverrideEnabled>false</CentralPackageVersionOverrideEnabled>
      </PropertyGroup>
      <ItemGroup>
        <PackageVersion Version='1.2.3' Include='AttributeOrder' />
        <PackageVersion Update="Multiline"
                        Version="2.3.4" />
        <PackageVersion Include="ChildVersion"><Version>3.4.5</Version></PackageVersion>
        <GlobalPackageReference Include="GlobalAttribute" Version="4.5.6" />
        <GlobalPackageReference Include="GlobalChild"><Version>5.6.7</Version></GlobalPackageReference>
        <PackageVersion Include="MissingVersion" />
        <PackageVersion Update='RangeVersion'><Version>[1.0.0,2.0.0)</Version></PackageVersion>
        <GlobalPackageReference Include="FloatingGlobal" Version="6.*" />
      </ItemGroup>
    </Project>`,
  }, async (rootUrl) => {
    assert.deepEqual(await validateDependencyPolicy(rootUrl), [
      'NuGet package MissingVersion is missing an exact version',
      'NuGet package RangeVersion is not exact: [1.0.0,2.0.0)',
      'NuGet package FloatingGlobal is not exact: 6.*',
    ]);
  });
});

test('central NuGet ownership properties cannot be weakened', async () => {
  await withPolicyFixture({
    centralPackages: `<Project>
      <PropertyGroup>
        <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
        <CentralPackageVersionOverrideEnabled>true</CentralPackageVersionOverrideEnabled>
      </PropertyGroup>
    </Project>`,
  }, async (rootUrl) => {
    assert.deepEqual(await validateDependencyPolicy(rootUrl), [
      'Directory.Packages.props must set ManagePackageVersionsCentrally to true',
      'Directory.Packages.props must set CentralPackageVersionOverrideEnabled to false',
    ]);
  });
});

test('PackageReference versions are rejected inside Directory.Packages.props', async () => {
  await withPolicyFixture({
    centralPackages: `<Project>
      <PropertyGroup>
        <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
        <CentralPackageVersionOverrideEnabled>false</CentralPackageVersionOverrideEnabled>
      </PropertyGroup>
      <ItemGroup>
        <PackageReference Include="CentralVersion" Version="1.2.3" />
        <PackageReference Include="CentralOverride"><VersionOverride>2.3.4</VersionOverride></PackageReference>
      </ItemGroup>
    </Project>`,
  }, async (rootUrl) => {
    assert.deepEqual(await validateDependencyPolicy(rootUrl), [
      'Directory.Packages.props PackageReference CentralVersion must not declare Version',
      'Directory.Packages.props PackageReference CentralOverride must not declare VersionOverride',
    ]);
  });
});

test('project files cannot disable central management or enable version overrides', async () => {
  await withPolicyFixture({
    nugetFiles: {
      'apps/cloud/Puntiro.Cloud.csproj': `<Project>
        <PropertyGroup>
          <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
          <CentralPackageVersionOverrideEnabled>true</CentralPackageVersionOverrideEnabled>
        </PropertyGroup>
      </Project>`,
    },
  }, async (rootUrl) => {
    assert.deepEqual(await validateDependencyPolicy(rootUrl), [
      'apps/cloud/Puntiro.Cloud.csproj must not disable central package version management',
      'apps/cloud/Puntiro.Cloud.csproj must not enable central package version overrides',
    ]);
  });
});

test('PackageReference declarations cannot carry local versions', async () => {
  await withPolicyFixture({
    nugetFiles: {
      'apps/cloud/Puntiro.Cloud.csproj': `<Project><ItemGroup>
        <PackageReference Include="AttributeVersion" Version="1.2.3" />
        <PackageReference Include="AttributeOverride" VersionOverride="2.3.4" />
        <PackageReference Include="ChildVersion"><Version>3.4.5</Version></PackageReference>
        <PackageReference Include="ChildOverride"><VersionOverride>4.5.6</VersionOverride></PackageReference>
      </ItemGroup></Project>`,
    },
  }, async (rootUrl) => {
    assert.deepEqual(await validateDependencyPolicy(rootUrl), [
      'apps/cloud/Puntiro.Cloud.csproj PackageReference AttributeVersion must not declare Version',
      'apps/cloud/Puntiro.Cloud.csproj PackageReference AttributeOverride must not declare VersionOverride',
      'apps/cloud/Puntiro.Cloud.csproj PackageReference ChildVersion must not declare Version',
      'apps/cloud/Puntiro.Cloud.csproj PackageReference ChildOverride must not declare VersionOverride',
    ]);
  });
});

test('central and global package declarations stay in Directory.Packages.props', async () => {
  await withPolicyFixture({
    nugetFiles: {
      'Directory.Build.props': `<Project><ItemGroup>
        <PackageVersion Include="MisplacedCentral" Version="1.2.3" />
        <GlobalPackageReference Include="MisplacedGlobal" Version="2.3.4" />
      </ItemGroup></Project>`,
    },
  }, async (rootUrl) => {
    assert.deepEqual(await validateDependencyPolicy(rootUrl), [
      'Directory.Build.props must not declare PackageVersion MisplacedCentral outside Directory.Packages.props',
      'Directory.Build.props must not declare GlobalPackageReference MisplacedGlobal outside Directory.Packages.props',
    ]);
  });
});

for (const [ignoredDirectory, relativeFile] of [
  ['.worktrees', '.worktrees/other-branch/apps/cloud/Puntiro.Cloud.csproj'],
  ['.pnpm-store', '.pnpm-store/v10/files/invalid/Directory.Build.props'],
]) {
  test(`ignores NuGet declarations under local ${ignoredDirectory} trees`, async () => {
    await withPolicyFixture({
      nugetFiles: {
        [relativeFile]: '<Project><ItemGroup><PackageReference Include="Ignored" Version="9.9.9" /></ItemGroup></Project>',
      },
    }, async (rootUrl) => {
      assert.deepEqual(await validateDependencyPolicy(rootUrl), []);
    });
  });
}

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
