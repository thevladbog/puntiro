import assert from 'node:assert/strict';
import { mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
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
  'docs/adr/0002-effective-friend-metadata-enforcement.md',
  'docs/adr/0003-global-identity-and-credentials.md',
  'docs/modules/identity.md',
  'docs/modules/tenancy.md',
  'docs/modules/integrations.md',
  'docs/runbooks/development-bootstrap.md',
  'docs/runbooks/cloud-development.md',
  'docs/runbooks/first-owner-provisioning.md',
  'docs/runbooks/owner-totp-recovery.md',
  'docs/runbooks/integration-token-rotation.md',
  'docs/reference/cloud-configuration.md',
  'docs/reference/cloud-authentication-api.md',
  'docs/engineering/cloud-identity-validation.md',
  'infra/compose/.env.cloud.example',
  'infra/compose/cloud-runtime.env.example',
  'docs/superpowers/specs/2026-08-08-puntiro-technical-architecture-design.md',
  'docs/superpowers/specs/2026-08-08-puntiro-cloud-identity-tenancy-design.md',
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
    const content = relativePath === 'AGENTS.md'
      ? agents
      : relativePath === 'docs/README.md'
        ? `
[Foundation](engineering/foundation-validation.md)
[Cloud validation](engineering/cloud-identity-validation.md)
[Identity](modules/identity.md)
[Tenancy](modules/tenancy.md)
[Integrations](modules/integrations.md)
[Global identity](adr/0003-global-identity-and-credentials.md)
[Cloud development](runbooks/cloud-development.md)
[First owner](runbooks/first-owner-provisioning.md)
[Owner recovery](runbooks/owner-totp-recovery.md)
[Token rotation](runbooks/integration-token-rotation.md)
[Cloud configuration](reference/cloud-configuration.md)
[Cloud authentication](reference/cloud-authentication-api.md)
`
        : relativePath === 'infra/compose/.env.cloud.example'
          ? 'PUNTIRO_CLOUD_IMAGE=\nPUNTIRO_CLOUD_RUNTIME_ENV_FILE=\n'
          : relativePath === 'infra/compose/cloud-runtime.env.example'
            ? 'ConnectionStrings__Puntiro=\nPuntiro__Security__SessionHmac__Keys__v1=\n'
            : relativePath === 'docs/reference/cloud-configuration.md'
              ? '`PUNTIRO_CLOUD_IMAGE`\n`PUNTIRO_CLOUD_RUNTIME_ENV_FILE`\n`ConnectionStrings__Puntiro`\n`Puntiro__Security__SessionHmac__Keys__v1`\n'
              : relativePath === 'docs/superpowers/specs/2026-08-08-puntiro-cloud-identity-tenancy-design.md'
                ? 'Статус: согласованный и реализованный дизайн этапа 1; автоматическая приёмка записана, deployment gates перечислены отдельно.\n'
        : '# Fixture\n';
    await writeFile(target, content);
  }));

  return { root, rootUrl: pathToFileURL(`${root}${path.sep}`) };
}

test('required repository documentation is complete', async () => {
  const errors = await validateRepositoryDocs(new URL('../', import.meta.url));
  assert.deepEqual(errors, []);
});

test('cloud identity documents are required and linked from the documentation map', async t => {
  const { root, rootUrl } = await createDocsFixture(t);
  await rm(path.join(root, 'docs/modules/integrations.md'));
  await writeFile(path.join(root, 'docs/README.md'), '[Foundation](engineering/foundation-validation.md)\n');

  const errors = await validateRepositoryDocs(rootUrl);
  assert.ok(errors.includes('Missing documentation: docs/modules/integrations.md'));
  assert.ok(errors.includes('docs/README.md must link modules/integrations.md'));
  assert.ok(errors.includes('docs/README.md must link reference/cloud-configuration.md'));
  assert.ok(errors.includes('docs/README.md must link engineering/cloud-identity-validation.md'));
});

test('every committed Cloud env-template variable is classified in the configuration reference', async t => {
  const { root, rootUrl } = await createDocsFixture(t);
  await writeFile(
    path.join(root, 'docs/reference/cloud-configuration.md'),
    '`ConnectionStrings__Puntiro`\n',
  );

  const errors = await validateRepositoryDocs(rootUrl);
  assert.ok(errors.includes(
    'docs/reference/cloud-configuration.md must document PUNTIRO_CLOUD_IMAGE from infra/compose/.env.cloud.example',
  ));
  assert.ok(errors.includes(
    'docs/reference/cloud-configuration.md must document Puntiro__Security__SessionHmac__Keys__v1 from infra/compose/cloud-runtime.env.example',
  ));
});

test('the implemented Cloud identity design has an accurate accepted status', async t => {
  const { root, rootUrl } = await createDocsFixture(t);
  await writeFile(
    path.join(root, 'docs/superpowers/specs/2026-08-08-puntiro-cloud-identity-tenancy-design.md'),
    'Статус: согласованный дизайн этапа 1; ожидает финального письменного review перед implementation plan.\n',
  );

  assert.ok((await validateRepositoryDocs(rootUrl)).includes(
    'Cloud identity design status must state that the approved stage is implemented and keep deployment gates separate',
  ));
});

test('foundation validation record is required and linked from the documentation map', async t => {
  const { root, rootUrl } = await createDocsFixture(t);
  const docsMap = await readFile(path.join(root, 'docs/README.md'), 'utf8');
  await writeFile(
    path.join(root, 'docs/README.md'),
    docsMap.replace('[Foundation](engineering/foundation-validation.md)\n', ''),
  );

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
