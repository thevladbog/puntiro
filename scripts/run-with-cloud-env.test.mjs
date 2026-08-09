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

test('rejects duplicate assignments in one env file without returning their values', async t => {
  const directory = await mkdtemp(path.join(os.tmpdir(), 'puntiro-cloud-env-'));
  t.after(() => rm(directory, { recursive: true, force: true }));
  const envFile = path.join(directory, 'runtime.env');
  await writeFile(envFile, 'DUPLICATE=first-review-probe\nDUPLICATE=second-review-probe\n');
  await chmod(envFile, 0o600);

  const result = spawnSync(process.execPath, [
    runner,
    '--env-file', envFile,
    '--',
    process.execPath,
    '--version',
  ], { cwd: root, encoding: 'utf8' });

  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /runtime\.env:2: duplicate dotenv assignment/);
  assert.doesNotMatch(result.stderr, /first-review-probe|second-review-probe/);
});

test('rejects quotes comments substitutions backticks and invalid value whitespace', async t => {
  const invalidAssignments = [
    'VALUE="quoted-review-probe"',
    "VALUE='quoted-review-probe'",
    'VALUE=bounded-review-probe # inline comment',
    'VALUE=$(bounded-review-probe)',
    'VALUE=${BOUNDED_REVIEW_PROBE}',
    'VALUE=$BOUNDED_REVIEW_PROBE',
    'VALUE=$1',
    'VALUE=$?',
    'VALUE=$$',
    'VALUE=$@',
    'VALUE=`bounded-review-probe`',
    'VALUE= leading-review-probe',
    'VALUE=trailing-review-probe ',
    'VALUE=tab\treview-probe',
  ];

  for (const [index, assignment] of invalidAssignments.entries()) {
    const directory = await mkdtemp(path.join(os.tmpdir(), 'puntiro-cloud-env-'));
    t.after(() => rm(directory, { recursive: true, force: true }));
    const envFile = path.join(directory, `runtime-${index}.env`);
    await writeFile(envFile, `${assignment}\n`);
    await chmod(envFile, 0o600);

    const result = spawnSync(process.execPath, [
      runner,
      '--env-file', envFile,
      '--',
      process.execPath,
      '--version',
    ], { cwd: root, encoding: 'utf8' });

    assert.notEqual(result.status, 0, assignment);
    assert.match(result.stderr, /invalid dotenv value syntax/, assignment);
    assert.doesNotMatch(result.stderr, /bounded-review-probe|quoted-review-probe|leading-review-probe|trailing-review-probe|tab\s+review-probe/);
  }
});

test('preserves raw semicolons internal spaces and base64 padding exactly under shell false', async t => {
  const directory = await mkdtemp(path.join(os.tmpdir(), 'puntiro-cloud-env-'));
  t.after(() => rm(directory, { recursive: true, force: true }));
  const envFile = path.join(directory, 'runtime.env');
  const connection = 'Host=127.0.0.1;Port=55440;Database=puntiro_restore_drill;Username=restore;Password=raw-secret;Include Error Detail=false';
  const key = Buffer.alloc(32, 0x54).toString('base64');
  await writeFile(envFile, `ConnectionStrings__Puntiro=${connection}\nHMAC_KEY=${key}\n`);
  await chmod(envFile, 0o600);

  const result = spawnSync(process.execPath, [
    runner,
    '--env-file', envFile,
    '--',
    process.execPath,
    '--eval',
    `process.exit(process.env.ConnectionStrings__Puntiro === ${JSON.stringify(connection)} && process.env.HMAC_KEY === ${JSON.stringify(key)} ? 0 : 24)`,
  ], { cwd: root, encoding: 'utf8' });

  assert.equal(result.status, 0, result.stderr);
  assert.equal(result.stdout, '');
});
