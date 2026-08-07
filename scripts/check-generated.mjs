import { readdir, readFile } from 'node:fs/promises';
import { spawnSync } from 'node:child_process';
import { relative, resolve } from 'node:path';

const repositoryRoot = resolve(import.meta.dirname, '..');
const distDirectory = resolve(repositoryRoot, 'packages/tokens/dist');

const snapshotDirectory = async (directory) => {
  const entries = await readdir(directory, { withFileTypes: true });
  const snapshot = new Map();

  for (const entry of entries.sort((left, right) => left.name.localeCompare(right.name))) {
    const entryPath = resolve(directory, entry.name);

    if (entry.isDirectory()) {
      for (const [path, bytes] of await snapshotDirectory(entryPath)) {
        snapshot.set(`${entry.name}/${path}`, bytes);
      }
    } else if (entry.isFile()) {
      snapshot.set(relative(directory, entryPath), await readFile(entryPath));
    } else {
      throw new Error(`Unsupported generated entry: ${relative(directory, entryPath)}`);
    }
  }

  return snapshot;
};

const before = await snapshotDirectory(distDirectory);
const pnpmExecutable = process.env.npm_execpath;
const pnpmVersion = process.env.npm_config_user_agent?.match(/^pnpm\/(\d+\.\d+\.\d+)/)?.[1];
const tokenBuildArguments = ['--filter', '@puntiro/tokens', 'build'];
const build = pnpmExecutable
  ? spawnSync(process.execPath, [pnpmExecutable, ...tokenBuildArguments], {
      cwd: repositoryRoot,
      stdio: 'inherit'
    })
  : pnpmVersion === '11.17.0'
    ? spawnSync('corepack', ['pnpm', ...tokenBuildArguments], {
        cwd: repositoryRoot,
        stdio: 'inherit'
      })
  : spawnSync(process.platform === 'win32' ? 'pnpm.cmd' : 'pnpm', tokenBuildArguments, {
      cwd: repositoryRoot,
      stdio: 'inherit'
    });

if (build.status !== 0) {
  process.exit(build.status ?? 1);
}

const after = await snapshotDirectory(distDirectory);
const differences = new Set([...before.keys(), ...after.keys()]);
const changed = [...differences].filter((path) => {
  const previous = before.get(path);
  const current = after.get(path);
  return previous === undefined || current === undefined || !previous.equals(current);
});

if (changed.length > 0) {
  console.error(`Generated token files are not reproducible: ${changed.join(', ')}`);
  process.exit(1);
}

console.log('Generated token files are reproducible.');
