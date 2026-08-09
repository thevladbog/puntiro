import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { chmod, mkdtemp, rm, symlink, writeFile } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const runner = path.join(root, 'scripts', 'run-with-cloud-env.mjs');

test('executes a command with an exact semicolon-delimited Npgsql value', async t => {
  const directory = await mkdtemp(path.join(os.tmpdir(), 'puntiro-cloud-env-'));
  t.after(() => rm(directory, { recursive: true, force: true }));
  const composeEnv = path.join(directory, 'compose.env');
  const runtimeEnv = path.join(directory, 'runtime.env');
  const connection =
    'Host=postgres;Port=5432;Database=puntiro_fixture;Username=puntiro_fixture;Include Error Detail=false';
  await writeFile(composeEnv, 'POSTGRES_DB=puntiro_fixture\n');
  await writeFile(runtimeEnv, `ConnectionStrings__Puntiro=${connection}\n`);
  await chmod(composeEnv, 0o600);
  await chmod(runtimeEnv, 0o600);

  const result = spawnSync(process.execPath, [
    runner,
    '--env-file', composeEnv,
    '--env-file', runtimeEnv,
    '--',
    process.execPath,
    '--eval',
    `process.exit(process.env.ConnectionStrings__Puntiro === ${JSON.stringify(connection)} ? 0 : 23)`,
  ], { cwd: root, encoding: 'utf8' });

  assert.equal(result.status, 0, result.stderr);
  assert.equal(result.stdout, '');
});

test('rejects shell syntax without returning the line value', async t => {
  const directory = await mkdtemp(path.join(os.tmpdir(), 'puntiro-cloud-env-'));
  t.after(() => rm(directory, { recursive: true, force: true }));
  const envFile = path.join(directory, 'runtime.env');
  await writeFile(envFile, 'export SECRET_VALUE=bounded-review-probe\n');
  await chmod(envFile, 0o600);

  const result = spawnSync(process.execPath, [
    runner,
    '--env-file', envFile,
    '--',
    process.execPath,
    '--version',
  ], { cwd: root, encoding: 'utf8' });

  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /runtime\.env:1: invalid dotenv assignment/);
  assert.doesNotMatch(result.stderr, /bounded-review-probe/);
});

test('rejects a symlinked secret env file instead of following it', async t => {
  const directory = await mkdtemp(path.join(os.tmpdir(), 'puntiro-cloud-env-'));
  t.after(() => rm(directory, { recursive: true, force: true }));
  const target = path.join(directory, 'private-target.env');
  const envFile = path.join(directory, 'runtime.env');
  await writeFile(target, 'SECRET_VALUE=bounded-review-probe\n');
  await chmod(target, 0o600);
  await symlink(target, envFile);

  const result = spawnSync(process.execPath, [
    runner,
    '--env-file', envFile,
    '--',
    process.execPath,
    '--version',
  ], { cwd: root, encoding: 'utf8' });

  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /runtime\.env: expected a regular env file/);
  assert.doesNotMatch(result.stderr, /bounded-review-probe/);
});
