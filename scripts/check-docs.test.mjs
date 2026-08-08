import assert from 'node:assert/strict';
import { mkdir, mkdtemp, rm, writeFile } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { pathToFileURL } from 'node:url';
import { validateRepositoryDocs } from './check-docs.mjs';

const requiredFixtureFiles = [
  'AGENTS.md',
  'README.md',
  'docs/README.md',
  'docs/adr/0001-modular-monolith-agent.md',
  'docs/runbooks/development-bootstrap.md',
  'docs/superpowers/specs/2026-08-08-puntiro-technical-architecture-design.md',
];

const agents = `# Puntiro Repository Instructions
## Architecture Invariants
## Required Commands
## Dependency Policy
## Documentation Policy
## Validation Boundaries
## Secrets and Logs
## Git Safety
`;

async function createDocsFixture(t) {
  const root = await mkdtemp(path.join(os.tmpdir(), 'puntiro-docs-'));
  t.after(() => rm(root, { force: true, recursive: true }));

  await Promise.all(requiredFixtureFiles.map(async relativePath => {
    const target = path.join(root, relativePath);
    await mkdir(path.dirname(target), { recursive: true });
    const content = relativePath === 'AGENTS.md' ? agents : '# Fixture\n';
    await writeFile(target, content);
  }));

  return { root, rootUrl: pathToFileURL(`${root}${path.sep}`) };
}

test('required repository documentation is complete', async () => {
  const errors = await validateRepositoryDocs(new URL('../', import.meta.url));
  assert.deepEqual(errors, []);
});

test('foundation validation record is required and linked from the documentation map', async t => {
  const { root, rootUrl } = await createDocsFixture(t);

  assert.deepEqual(await validateRepositoryDocs(rootUrl), [
    'Missing documentation: docs/engineering/foundation-validation.md',
    'docs/README.md must link engineering/foundation-validation.md',
  ]);

  const validationPath = path.join(root, 'docs/engineering/foundation-validation.md');
  await mkdir(path.dirname(validationPath), { recursive: true });
  await writeFile(validationPath, '# Foundation validation\n');

  assert.deepEqual(await validateRepositoryDocs(rootUrl), [
    'docs/README.md must link engineering/foundation-validation.md',
  ]);
});
