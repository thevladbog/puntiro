import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { chmod, mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const validator = path.join(root, 'scripts', 'validate-cloud-runtime-env.mjs');

async function restoreFixture(t, database = 'puntiro_restore_drill') {
  const directory = await mkdtemp(path.join(os.tmpdir(), 'puntiro-cloud-restore-'));
  t.after(() => rm(directory, { recursive: true, force: true }));
  const composeEnv = path.join(directory, 'compose.restore.env');
  const runtimeEnv = path.join(directory, 'runtime.restore.env');
  const certificate = path.join(directory, 'certificate.pfx');
  const keyRing = path.join(directory, 'data-protection-keys');
  const keys = {
    session: Buffer.alloc(32, 0x51).toString('base64'),
    recovery: Buffer.alloc(32, 0x52).toString('base64'),
    integration: Buffer.alloc(32, 0x53).toString('base64'),
  };
  await mkdir(keyRing, { mode: 0o700 });
  await writeFile(certificate, 'fixture-only-certificate-material', { mode: 0o600 });
  await writeFile(composeEnv, [
    'PUNTIRO_RESTORE_PROJECT=puntiro-restore-drill',
    'POSTGRES_DB=puntiro_restore_drill',
    'POSTGRES_USER=puntiro_restore_fixture',
    'POSTGRES_PASSWORD=fixture-only',
    'PUNTIRO_POSTGRES_PORT=55440',
    `PUNTIRO_CLOUD_RUNTIME_ENV_FILE=${runtimeEnv}`,
    `Puntiro__Security__DataProtectionCertificateHostPath=${certificate}`,
    '',
  ].join('\n'));
  await writeFile(runtimeEnv, [
    `ConnectionStrings__Puntiro=Host=postgres;Port=5432;Database=${database};Username=puntiro_restore_fixture;Password=fixture-only;Include Error Detail=false`,
    'Puntiro__Admin__AllowedOrigin=https://restore.invalid/',
    `Puntiro__Security__DataProtectionKeysPath=${keyRing}`,
    `Puntiro__Security__DataProtectionCertificatePath=${certificate}`,
    'Puntiro__Security__DataProtectionCertificatePassword=fixture-only',
    'Puntiro__Security__SessionHmac__CurrentVersion=v1',
    `Puntiro__Security__SessionHmac__Keys__v0=${Buffer.alloc(32, 0x50).toString('base64')}`,
    `Puntiro__Security__SessionHmac__Keys__v1=${keys.session}`,
    'Puntiro__Security__RecoveryHmac__CurrentVersion=v1',
    `Puntiro__Security__RecoveryHmac__Keys__v1=${keys.recovery}`,
    'Puntiro__Security__IntegrationHmac__CurrentVersion=v1',
    `Puntiro__Security__IntegrationHmac__Keys__v1=${keys.integration}`,
    '',
  ].join('\n'));
  await chmod(composeEnv, 0o600);
  await chmod(runtimeEnv, 0o600);
  return { certificate, composeEnv, keyRing, runtimeEnv };
}

function runValidator({ composeEnv, runtimeEnv }) {
  return spawnSync(process.execPath, [
    validator,
    '--compose-env', composeEnv,
    '--runtime-env', runtimeEnv,
    '--restore-project', 'puntiro-restore-drill',
  ], { cwd: root, encoding: 'utf8' });
}

test('accepts a restore-only project whose runtime connection targets its restored database', async t => {
  const fixture = await restoreFixture(t);

  const result = runValidator(fixture);

  assert.equal(result.status, 0, result.stderr);
  assert.equal(result.stdout, 'Cloud restore environment contract passed.\n');
});

test('rejects a normal database target before Compose can create or start restore services', async t => {
  const fixture = await restoreFixture(t, 'puntiro_normal');

  const result = runValidator(fixture);

  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /runtime connection database must equal the isolated restore database/);
  assert.doesNotMatch(result.stderr, /Password=|fixture-only/);
});

test('rejects a certificate mount that differs from the host certificate validated for restore', async t => {
  const fixture = await restoreFixture(t);
  const contents = await readFile(fixture.runtimeEnv, 'utf8');
  await writeFile(
    fixture.runtimeEnv,
    contents.replace(
      `Puntiro__Security__DataProtectionCertificatePath=${fixture.certificate}`,
      `Puntiro__Security__DataProtectionCertificatePath=${path.join(path.dirname(fixture.certificate), 'other.pfx')}`,
    ),
  );
  await chmod(fixture.runtimeEnv, 0o600);

  const result = runValidator(fixture);

  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /certificate path must equal the validated host certificate path/);
  assert.doesNotMatch(result.stderr, /fixture-only/);
});

test('rejects missing key material before Compose can create or start restore services', async t => {
  const fixture = await restoreFixture(t);
  await rm(fixture.keyRing, { recursive: true });

  const result = runValidator(fixture);

  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /Data Protection key ring must be an existing private directory/);
  assert.doesNotMatch(result.stderr, /fixture-only/);
});

test('validates every retained HMAC version rather than only the current version', async t => {
  const fixture = await restoreFixture(t);
  const contents = await readFile(fixture.runtimeEnv, 'utf8');
  await writeFile(
    fixture.runtimeEnv,
    contents.replace(
      /^Puntiro__Security__SessionHmac__Keys__v0=.*$/m,
      'Puntiro__Security__SessionHmac__Keys__v0=not-canonical-base64',
    ),
  );
  await chmod(fixture.runtimeEnv, 0o600);

  const result = runValidator(fixture);

  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /Session HMAC retained key format is invalid/);
  assert.doesNotMatch(result.stderr, /not-canonical-base64/);
});

test('rejects HMAC key reuse across purposes before creating restore services', async t => {
  const fixture = await restoreFixture(t);
  const contents = await readFile(fixture.runtimeEnv, 'utf8');
  const sessionKey = contents.match(/^Puntiro__Security__SessionHmac__Keys__v1=(.*)$/m)?.[1];
  assert.ok(sessionKey);
  await writeFile(
    fixture.runtimeEnv,
    contents.replace(
      /^Puntiro__Security__RecoveryHmac__Keys__v1=.*$/m,
      `Puntiro__Security__RecoveryHmac__Keys__v1=${sessionKey}`,
    ),
  );
  await chmod(fixture.runtimeEnv, 0o600);

  const result = runValidator(fixture);

  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /HMAC key material must be distinct across purposes/);
  assert.equal(
    result.stderr.includes(sessionKey),
    false,
    'validator stderr must not contain HMAC material',
  );
});
