import { readFile, readdir } from 'node:fs/promises';
import { fileURLToPath, pathToFileURL } from 'node:url';
import path from 'node:path';

const approvedActions = new Map([
  ['actions/checkout', {
    sha: '3d3c42e5aac5ba805825da76410c181273ba90b1',
    count: 2,
  }],
  ['actions/setup-node', {
    sha: '820762786026740c76f36085b0efc47a31fe5020',
    count: 2,
  }],
  ['actions/setup-dotnet', {
    sha: 'a98b56852c35b8e3190ac28c8c2271da59106c68',
    count: 2,
  }],
]);

const expectedAuditCommand = 'corepack pnpm audit --audit-level high && dotnet package list --project Puntiro.slnx --vulnerable --include-transitive';
const expectedDotnetCommand = 'node scripts/check-dotnet.mjs';
const expectedFoundationCommand = 'corepack pnpm docs:check && corepack pnpm dependencies:check && corepack pnpm dependencies:audit && corepack pnpm test:repository && corepack pnpm foundation:check && corepack pnpm --filter @puntiro/ui build && corepack pnpm --filter @puntiro/admin typecheck && corepack pnpm --filter @puntiro/kiosk-web typecheck && corepack pnpm --filter @puntiro/admin build && corepack pnpm --filter @puntiro/kiosk-web build && corepack pnpm check:dotnet';

async function workflowFiles(root) {
  const workflowDirectory = path.join(root, '.github', 'workflows');
  const entries = await readdir(workflowDirectory, { withFileTypes: true });
  return entries
    .filter(entry => entry.isFile() && ['.yaml', '.yml'].includes(path.extname(entry.name)))
    .map(entry => path.posix.join('.github', 'workflows', entry.name))
    .sort();
}

function usesEntries(content) {
  const entries = [];
  for (const line of content.split(/\r?\n/)) {
    const uncommented = line.replace(/\s+#.*$/, '');
    const match = uncommented.match(/^\s*(?:-\s*)?uses\s*:\s*(.*?)\s*$/);
    if (!match) continue;
    entries.push(match[1].replace(/^(['"])(.*)\1$/, '$2'));
  }
  return entries;
}

function jobBlock(content, jobName) {
  const lines = content.split(/\r?\n/);
  const start = lines.findIndex(line => line === `  ${jobName}:`);
  if (start === -1) return undefined;
  const nextJob = lines.findIndex((line, index) =>
    index > start && /^  [A-Za-z0-9_-]+:\s*$/.test(line));
  return lines.slice(start, nextJob === -1 ? lines.length : nextJob).join('\n');
}

function runCommands(block) {
  const commands = [];
  for (const line of block.split(/\r?\n/)) {
    const match = line.match(/^\s*-\s+run:\s*(.*?)\s*$/);
    if (match) commands.push(match[1]);
  }
  return commands;
}

function requireJobLine(errors, jobName, block, line) {
  if (!block.split(/\r?\n/).some(candidate => candidate.trim() === line)) {
    errors.push(`${jobName} job must declare: ${line}`);
  }
}

function requireJobCommands(errors, jobName, block, requiredCommands) {
  const commands = new Set(runCommands(block));
  for (const command of requiredCommands) {
    if (!commands.has(command)) errors.push(`${jobName} job must run: ${command}`);
  }
}

function validateFoundationWorkflow(content) {
  const errors = [];
  const repositoryJob = jobBlock(content, 'repository-contracts');
  const windowsJob = jobBlock(content, 'windows-build');

  if (!repositoryJob) {
    errors.push('foundation workflow must define repository-contracts job');
  } else {
    requireJobLine(errors, 'repository-contracts', repositoryJob, 'name: Repository contracts');
    requireJobLine(errors, 'repository-contracts', repositoryJob, 'runs-on: ubuntu-latest');
    requireJobLine(errors, 'repository-contracts', repositoryJob, 'node-version: 24.19.0');
    requireJobLine(errors, 'repository-contracts', repositoryJob, 'dotnet-version: 10.0.302');
    requireJobCommands(errors, 'repository-contracts', repositoryJob, [
      'corepack pnpm install --frozen-lockfile',
      'corepack pnpm check:foundation',
    ]);
  }

  if (!windowsJob) {
    errors.push('foundation workflow must define windows-build job');
  } else {
    requireJobLine(errors, 'windows-build', windowsJob, 'name: Windows compile');
    requireJobLine(errors, 'windows-build', windowsJob, 'runs-on: windows-latest');
    requireJobLine(errors, 'windows-build', windowsJob, 'node-version: 24.19.0');
    requireJobLine(errors, 'windows-build', windowsJob, 'dotnet-version: 10.0.302');
    requireJobCommands(errors, 'windows-build', windowsJob, [
      'node scripts/check-dotnet.mjs',
    ]);
  }

  return errors;
}

export async function validateCiContract(rootUrl) {
  const root = fileURLToPath(rootUrl);
  const errors = [];
  const counts = new Map([...approvedActions.keys()].map(action => [action, 0]));
  let workflows;

  try {
    workflows = await workflowFiles(root);
  } catch {
    errors.push('Missing CI workflows directory: .github/workflows');
    workflows = [];
  }

  const contents = new Map();
  for (const relativePath of workflows) {
    const content = await readFile(path.join(root, relativePath), 'utf8');
    contents.set(relativePath, content);
    for (const reference of usesEntries(content)) {
      const separator = reference.lastIndexOf('@');
      const action = separator === -1 ? reference : reference.slice(0, separator);
      const approval = approvedActions.get(action);
      if (!approval) {
        errors.push(`${relativePath} uses unapproved action ${reference}`);
        continue;
      }
      counts.set(action, counts.get(action) + 1);
      if (reference !== `${action}@${approval.sha}`) {
        errors.push(`${relativePath} uses ${reference} instead of the approved immutable SHA`);
      }
    }
  }

  for (const [action, approval] of approvedActions) {
    const count = counts.get(action);
    if (count !== approval.count) {
      errors.push(`${action} must occur exactly ${approval.count} time(s), received ${count}`);
    }
  }

  const foundationWorkflow = contents.get('.github/workflows/foundation.yml');
  if (!foundationWorkflow) {
    errors.push('Missing CI workflow: .github/workflows/foundation.yml');
  } else {
    errors.push(...validateFoundationWorkflow(foundationWorkflow));
  }

  try {
    const manifest = JSON.parse(await readFile(path.join(root, 'package.json'), 'utf8'));
    if (manifest.scripts?.['dependencies:audit'] !== expectedAuditCommand) {
      errors.push(
        'package.json dependencies:audit must audit all npm dependencies and transitive NuGet packages',
      );
    }
    if (manifest.scripts?.['check:dotnet'] !== expectedDotnetCommand) {
      errors.push('package.json check:dotnet must invoke the definitive fresh-output policy chain');
    }
    if (Object.hasOwn(manifest.scripts ?? {}, 'internals:check')) {
      errors.push('package.json must not expose fixed-path internals:check');
    }
    if (manifest.scripts?.['check:foundation'] !== expectedFoundationCommand) {
      errors.push('package.json check:foundation does not match the approved aggregate command');
    }
  } catch {
    errors.push('Missing or invalid package.json for CI contract');
  }

  return errors;
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? '').href) {
  const errors = await validateCiContract(new URL('../', import.meta.url));
  if (errors.length > 0) {
    for (const error of errors) console.error(error);
    process.exitCode = 1;
  }
}
