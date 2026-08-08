import assert from 'node:assert/strict';
import { mkdir, mkdtemp, rm, writeFile } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { pathToFileURL } from 'node:url';
import { validateCiContract } from './check-ci-contract.mjs';

const checkout = 'actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1';
const setupNode = 'actions/setup-node@820762786026740c76f36085b0efc47a31fe5020';
const setupDotnet = 'actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68';

const validWorkflow = `name: Repository foundation
jobs:
  repository-contracts:
    name: Repository contracts
    runs-on: ubuntu-latest
    steps:
      - uses: ${checkout}
      - uses: ${setupNode}
        with:
          node-version: 24.19.0
      - uses: ${setupDotnet}
        with:
          dotnet-version: 10.0.302
      - run: corepack pnpm install --frozen-lockfile
      - run: corepack pnpm check:foundation
      - run: dotnet restore Puntiro.slnx
      - run: dotnet build Puntiro.slnx --configuration Release --no-restore
  windows-build:
    name: Windows compile
    runs-on: windows-latest
    steps:
      - uses: ${checkout}
      - uses: ${setupDotnet}
        with:
          dotnet-version: 10.0.302
      - run: dotnet restore Puntiro.slnx
      - run: dotnet build Puntiro.slnx --configuration Release --no-restore
`;

const validManifest = {
  scripts: {
    'dependencies:audit': 'corepack pnpm audit --audit-level high && dotnet package list --project Puntiro.slnx --vulnerable --include-transitive',
    'internals:check': 'dotnet run --project tools/Puntiro.AssemblyPolicy/Puntiro.AssemblyPolicy.csproj --configuration Release --no-build -- src/Puntiro.Security/bin/Release/net10.0/Puntiro.Security.dll src/Puntiro.Modules.Identity/bin/Release/net10.0/Puntiro.Modules.Identity.dll src/Puntiro.Modules.Tenancy/bin/Release/net10.0/Puntiro.Modules.Tenancy.dll src/Puntiro.Modules.Integrations/bin/Release/net10.0/Puntiro.Modules.Integrations.dll tools/Puntiro.Provisioning/bin/Release/net10.0/Puntiro.Provisioning.dll',
    'check:dotnet': 'dotnet build Puntiro.slnx --configuration Release && corepack pnpm internals:check',
    'check:foundation': 'corepack pnpm docs:check && corepack pnpm dependencies:check && corepack pnpm dependencies:audit && corepack pnpm test:repository && corepack pnpm foundation:check && corepack pnpm --filter @puntiro/ui build && corepack pnpm --filter @puntiro/admin typecheck && corepack pnpm --filter @puntiro/kiosk-web typecheck && corepack pnpm --filter @puntiro/admin build && corepack pnpm --filter @puntiro/kiosk-web build && corepack pnpm check:dotnet',
  },
};

async function withCiFixture(t, workflow = validWorkflow, manifest = validManifest) {
  const root = await mkdtemp(path.join(os.tmpdir(), 'puntiro-ci-contract-'));
  t.after(() => rm(root, { force: true, recursive: true }));
  await mkdir(path.join(root, '.github/workflows'), { recursive: true });
  await writeFile(path.join(root, '.github/workflows/foundation.yml'), workflow);
  await writeFile(path.join(root, 'package.json'), JSON.stringify(manifest));
  return { root, rootUrl: pathToFileURL(`${root}${path.sep}`) };
}

test('foundation CI and aggregate command satisfy the immutable contract', async () => {
  assert.deepEqual(await validateCiContract(new URL('../', import.meta.url)), []);
});

test('rejects tags, wrong SHAs, and unknown actions in every workflow', async t => {
  const { root, rootUrl } = await withCiFixture(
    t,
    validWorkflow
      .replace(checkout, 'actions/checkout@v7.0.1')
      .concat('  extra-job:\n    uses: vendor/unknown-action@0123456789012345678901234567890123456789\n'),
  );
  await writeFile(
    path.join(root, '.github/workflows/extra.yaml'),
    'jobs:\n  wrong-sha:\n    uses: actions/setup-node@1111111111111111111111111111111111111111\n',
  );

  const errors = await validateCiContract(rootUrl);
  assert.ok(errors.includes('.github/workflows/foundation.yml uses actions/checkout@v7.0.1 instead of the approved immutable SHA'));
  assert.ok(errors.includes('.github/workflows/foundation.yml uses unapproved action vendor/unknown-action@0123456789012345678901234567890123456789'));
  assert.ok(errors.includes('.github/workflows/extra.yaml uses actions/setup-node@1111111111111111111111111111111111111111 instead of the approved immutable SHA'));
});

test('rejects duplicate and missing approved action occurrences', async t => {
  const duplicate = validWorkflow.replace(
    `      - uses: ${setupNode}\n`,
    `      - uses: ${setupNode}\n      - uses: ${setupNode}\n`,
  );
  const duplicateFixture = await withCiFixture(t, duplicate);
  assert.ok((await validateCiContract(duplicateFixture.rootUrl)).includes(
    'actions/setup-node must occur exactly 1 time(s), received 2',
  ));

  const missing = validWorkflow.replace(`      - uses: ${setupDotnet}\n`, '');
  const missingFixture = await withCiFixture(t, missing);
  assert.ok((await validateCiContract(missingFixture.rootUrl)).includes(
    'actions/setup-dotnet must occur exactly 2 time(s), received 1',
  ));
});

test('requires aggregate, restore, and Release build commands in their platform jobs', async t => {
  const workflow = validWorkflow
    .replace('      - run: corepack pnpm check:foundation\n', '')
    .replaceAll(
      '      - run: dotnet restore Puntiro.slnx\n',
      '      - run: dotnet restore Puntiro.slnx --locked-mode\n',
    )
    .replaceAll(
      '      - run: dotnet build Puntiro.slnx --configuration Release --no-restore\n',
      '      - run: dotnet build Puntiro.slnx --configuration Debug --no-restore\n',
    );
  const { rootUrl } = await withCiFixture(t, workflow);

  const errors = await validateCiContract(rootUrl);
  assert.ok(errors.includes('repository-contracts job must run: corepack pnpm check:foundation'));
  assert.ok(errors.includes('repository-contracts job must run: dotnet restore Puntiro.slnx'));
  assert.ok(errors.includes('repository-contracts job must run: dotnet build Puntiro.slnx --configuration Release --no-restore'));
  assert.ok(errors.includes('windows-build job must run: dotnet restore Puntiro.slnx'));
  assert.ok(errors.includes('windows-build job must run: dotnet build Puntiro.slnx --configuration Release --no-restore'));
});

test('rejects an audit or aggregate command that drifts from the exact contract', async t => {
  const manifest = structuredClone(validManifest);
  manifest.scripts['dependencies:audit'] = 'corepack pnpm audit --prod --audit-level high';
  manifest.scripts['check:foundation'] = 'corepack pnpm test:repository';
  const { rootUrl } = await withCiFixture(t, validWorkflow, manifest);

  assert.deepEqual((await validateCiContract(rootUrl)).filter(error => error.startsWith('package.json')), [
    'package.json dependencies:audit must audit all npm dependencies and transitive NuGet packages',
    'package.json check:foundation does not match the approved aggregate command',
  ]);
});

test('rejects .NET checks that build without verifying final assembly metadata', async t => {
  const manifest = structuredClone(validManifest);
  manifest.scripts['check:dotnet'] = 'dotnet build Puntiro.slnx --configuration Release';
  manifest.scripts['internals:check'] = 'node scripts/check-foundation.mjs';
  const { rootUrl } = await withCiFixture(t, validWorkflow, manifest);

  assert.deepEqual((await validateCiContract(rootUrl)).filter(error => error.startsWith('package.json')), [
    'package.json check:dotnet must build and then verify final assembly metadata',
    'package.json internals:check must inspect every protected Release assembly',
  ]);
});
