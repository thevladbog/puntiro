import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { chmod, mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const preflight = path.join(root, 'scripts', 'preflight-cloud-runtime.mjs');

async function runtimeFixture(t) {
  const directory = await mkdtemp(path.join(os.tmpdir(), 'puntiro-cloud-preflight-'));
  t.after(() => rm(directory, { recursive: true, force: true }));
  const composeEnv = path.join(directory, 'compose.env');
  const runtimeEnv = path.join(directory, 'runtime.env');
  const ring = path.join(directory, 'ring');
  const certificate = path.join(directory, 'certificate.pfx');
  const keyId = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee';
  const keyFile = path.join(ring, `key-${keyId}.xml`);
  await mkdir(ring, { mode: 0o700 });
  await writeFile(keyFile, `<?xml version="1.0"?><key id="${keyId}" version="1"><creationDate>2026-08-09T00:00:00Z</creationDate><activationDate>2026-08-09T00:00:00Z</activationDate><expirationDate>2026-11-09T00:00:00Z</expirationDate><descriptor deserializerType="fixture"><encryptedSecret decryptorType="fixture" /></descriptor></key>`, { mode: 0o600 });
  await writeFile(certificate, Buffer.from([0x30, 0x82, 0x00, 0x01, 0x00]), { mode: 0o600 });
  await writeFile(composeEnv, [
    `PUNTIRO_CLOUD_RUNTIME_ENV_FILE=${runtimeEnv}`,
    `PUNTIRO_CLOUD_UID=${process.getuid()}`,
    `PUNTIRO_CLOUD_GID=${process.getgid()}`,
    `Puntiro__Security__DataProtectionCertificateHostPath=${certificate}`,
    '',
  ].join('\n'));
  await writeFile(runtimeEnv, [
    `Puntiro__Security__DataProtectionKeysPath=${ring}`,
    `Puntiro__Security__DataProtectionCertificatePath=${certificate}`,
    '',
  ].join('\n'));
  await chmod(composeEnv, 0o600);
  await chmod(runtimeEnv, 0o600);
  return { certificate, composeEnv, keyFile, ring, runtimeEnv };
}

function runPreflight(fixture) {
  return spawnSync(process.execPath, [
    preflight,
    '--compose-env', fixture.composeEnv,
    '--runtime-env', fixture.runtimeEnv,
  ], { cwd: root, encoding: 'utf8' });
}

test('accepts a private nonempty runtime environment owned for the configured service identity', async t => {
  const fixture = await runtimeFixture(t);

  const result = runPreflight(fixture);

  assert.equal(result.status, 0, result.stderr);
  assert.equal(result.stdout, 'Cloud runtime preflight passed.\n');
});

test('resolves the configured runtime env relative to the Compose env directory', async t => {
  const fixture = await runtimeFixture(t);
  const contents = await readFile(fixture.composeEnv, 'utf8');
  await writeFile(
    fixture.composeEnv,
    contents.replace(`PUNTIRO_CLOUD_RUNTIME_ENV_FILE=${fixture.runtimeEnv}`, 'PUNTIRO_CLOUD_RUNTIME_ENV_FILE=runtime.env'),
  );
  await chmod(fixture.composeEnv, 0o600);

  const result = runPreflight(fixture);

  assert.equal(result.status, 0, result.stderr);
});

test('rejects an empty Data Protection ring before normal Cloud startup', async t => {
  const fixture = await runtimeFixture(t);
  await rm(fixture.keyFile);

  const result = runPreflight(fixture);

  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /Data Protection key ring must contain a valid protected key/);
});

test('rejects runtime material not owned by the configured service uid and gid', async t => {
  const fixture = await runtimeFixture(t);
  const contents = await readFile(fixture.composeEnv, 'utf8');
  await writeFile(
    fixture.composeEnv,
    contents.replace(`PUNTIRO_CLOUD_UID=${process.getuid()}`, `PUNTIRO_CLOUD_UID=${process.getuid() + 1}`),
  );
  await chmod(fixture.composeEnv, 0o600);

  const result = runPreflight(fixture);

  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /runtime key ring and certificate must be owned by the configured Cloud uid and gid/);
});

test('rejects permissive runtime env ring and certificate modes', async t => {
  const cases = [
    ['runtime env', async fixture => chmod(fixture.runtimeEnv, 0o640), /runtime\.env: permissions must be 0600 or stricter/],
    ['ring', async fixture => chmod(fixture.ring, 0o750), /Data Protection key ring must be mode 0700/],
    ['certificate', async fixture => chmod(fixture.certificate, 0o640), /Data Protection certificate must be mode 0600/],
  ];

  for (const [name, mutate, expected] of cases) {
    const fixture = await runtimeFixture(t);
    await mutate(fixture);
    const result = runPreflight(fixture);
    assert.notEqual(result.status, 0, name);
    assert.match(result.stderr, expected, name);
  }
});
