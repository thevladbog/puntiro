import { access, readFile } from 'node:fs/promises';
import { fileURLToPath, pathToFileURL } from 'node:url';
import path from 'node:path';

const requiredFiles = [
  'AGENTS.md',
  'README.md',
  'docs/README.md',
  'docs/adr/0001-modular-monolith-agent.md',
  'docs/engineering/foundation-validation.md',
  'docs/runbooks/development-bootstrap.md',
  'docs/superpowers/specs/2026-08-08-puntiro-technical-architecture-design.md'
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
    if (!/\]\(engineering\/foundation-validation\.md(?:#[^)]+)?\)/.test(docsMap)) {
      errors.push('docs/README.md must link engineering/foundation-validation.md');
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
