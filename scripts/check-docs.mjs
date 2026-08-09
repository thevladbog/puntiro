import { access, readFile } from 'node:fs/promises';
import { fileURLToPath, pathToFileURL } from 'node:url';
import path from 'node:path';

const requiredFiles = [
  'AGENTS.md',
  'README.md',
  'docs/README.md',
  'docs/adr/0001-modular-monolith-agent.md',
  'docs/adr/0002-effective-friend-metadata-enforcement.md',
  'docs/adr/0003-global-identity-and-credentials.md',
  'docs/engineering/foundation-validation.md',
  'docs/engineering/cloud-identity-validation.md',
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
  'infra/compose/.env.cloud.example',
  'infra/compose/cloud-runtime.env.example',
  'docs/superpowers/specs/2026-08-08-puntiro-technical-architecture-design.md',
  'docs/superpowers/specs/2026-08-08-puntiro-cloud-identity-tenancy-design.md'
];

const agentsHeadings = [
  '# Puntiro Repository Instructions',
  '## Architecture Invariants',
  '## Required Commands',
  '## Dependency Policy',
  '## Documentation Policy',
  '## Validation Boundaries',
  '## Secrets and Logs',
  '## Git Safety'
];

const docsMapLinks = [
  'engineering/foundation-validation.md',
  'engineering/cloud-identity-validation.md',
  'adr/0003-global-identity-and-credentials.md',
  'modules/identity.md',
  'modules/tenancy.md',
  'modules/integrations.md',
  'runbooks/cloud-development.md',
  'runbooks/first-owner-provisioning.md',
  'runbooks/owner-totp-recovery.md',
  'runbooks/integration-token-rotation.md',
  'reference/cloud-configuration.md',
  'reference/cloud-authentication-api.md',
];

export async function validateRepositoryDocs(rootUrl) {
  const root = fileURLToPath(rootUrl);
  const errors = [];

  for (const relativePath of requiredFiles) {
    try {
      await access(path.join(root, relativePath));
    } catch {
      errors.push(`Missing documentation: ${relativePath}`);
    }
  }

  try {
    const agents = await readFile(path.join(root, 'AGENTS.md'), 'utf8');
    for (const heading of agentsHeadings) {
      if (!agents.includes(heading)) errors.push(`AGENTS.md missing heading: ${heading}`);
    }
  } catch {
    // Missing-file diagnostic is already emitted above.
  }

  try {
    const docsMap = await readFile(path.join(root, 'docs/README.md'), 'utf8');
    for (const relativePath of docsMapLinks) {
      const escaped = relativePath.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
      if (!new RegExp(`\\]\\(${escaped}(?:#[^)]+)?\\)`).test(docsMap)) {
        errors.push(`docs/README.md must link ${relativePath}`);
      }
    }
  } catch {
    // Missing-file diagnostic is already emitted above.
  }

  try {
    const reference = await readFile(path.join(root, 'docs/reference/cloud-configuration.md'), 'utf8');
    for (const relativePath of [
      'infra/compose/.env.cloud.example',
      'infra/compose/cloud-runtime.env.example',
    ]) {
      const template = await readFile(path.join(root, relativePath), 'utf8');
      const variables = template.split(/\r?\n/)
        .map(line => line.match(/^([A-Za-z_][A-Za-z0-9_-]*)=/)?.[1])
        .filter(Boolean);
      for (const variable of variables) {
        if (!reference.includes(`\`${variable}\``)) {
          errors.push(`docs/reference/cloud-configuration.md must document ${variable} from ${relativePath}`);
        }
      }
    }
  } catch {
    // Missing-file diagnostics are already emitted above.
  }

  try {
    const design = await readFile(
      path.join(root, 'docs/superpowers/specs/2026-08-08-puntiro-cloud-identity-tenancy-design.md'),
      'utf8',
    );
    const status = design.split(/\r?\n/).find(line => line.startsWith('Статус:')) ?? '';
    if (!/согласованн.*реализованн/i.test(status) ||
        !/(?:deployment|разв[её]рт)/i.test(status) ||
        !/(?:отдельн|not run)/i.test(status)) {
      errors.push('Cloud identity design status must state that the approved stage is implemented and keep deployment gates separate');
    }
  } catch {
    // Missing-file diagnostic is already emitted above.
  }

  return errors;
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? '').href) {
  const errors = await validateRepositoryDocs(new URL('../', import.meta.url));
  if (errors.length > 0) {
    for (const error of errors) console.error(error);
    process.exitCode = 1;
  }
}
