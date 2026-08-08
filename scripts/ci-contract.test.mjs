import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { test } from 'node:test';

test('foundation CI separates repository and Windows compilation evidence', async () => {
  const workflow = await readFile(new URL('../.github/workflows/foundation.yml', import.meta.url), 'utf8');
  assert.match(workflow, /name: Repository contracts/);
  assert.match(workflow, /name: Windows compile/);
  assert.match(workflow, /node-version: 24\.19\.0/);
  assert.match(workflow, /dotnet-version: 10\.0\.302/);
  assert.match(workflow, /uses: actions\/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7\.0\.1/);
  assert.match(workflow, /uses: actions\/setup-node@820762786026740c76f36085b0efc47a31fe5020 # v7\.0\.0/);
  assert.match(workflow, /uses: actions\/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6\.0\.0/);
  assert.match(workflow, /corepack pnpm install --frozen-lockfile/);
  assert.doesNotMatch(workflow, /Windows acceptance/);
});

test('foundation vulnerability audit uses the configured npm and NuGet sources', async () => {
  const manifest = JSON.parse(await readFile(new URL('../package.json', import.meta.url), 'utf8'));
  assert.equal(
    manifest.scripts['dependencies:audit'],
    'corepack pnpm audit --prod --audit-level high && dotnet package list --project Puntiro.slnx --vulnerable --include-transitive',
  );
});
