import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { chmod, mkdir, mkdtemp, readFile, rm, symlink, writeFile } from 'node:fs/promises';
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
  const bin = path.join(directory, 'bin');
  const certificate = path.join(directory, 'certificate.pfx');
  const keyRing = path.join(directory, 'data-protection-keys');
  const keyId = '11111111-2222-3333-4444-555555555555';
  const keyFile = path.join(keyRing, `key-${keyId}.xml`);
  const keys = {
    session: Buffer.alloc(32, 0x51).toString('base64'),
    recovery: Buffer.alloc(32, 0x52).toString('base64'),
    integration: Buffer.alloc(32, 0x53).toString('base64'),
  };
  await mkdir(keyRing, { mode: 0o700 });
  await mkdir(bin);
  await writeFile(keyFile, `<?xml version="1.0" encoding="utf-8"?>
<key id="${keyId}" version="1">
  <creationDate>2026-08-09T00:00:00.0000000Z</creationDate>
  <activationDate>2026-08-09T00:00:00.0000000Z</activationDate>
  <expirationDate>2026-11-07T00:00:00.0000000Z</expirationDate>
  <descriptor deserializerType="fixture"><encryptedSecret decryptorType="fixture"><value>fixture</value></encryptedSecret></descriptor>
</key>
`, { mode: 0o600 });
  await writeFile(certificate, Buffer.from([0x30, 0x82, 0x00, 0x01, 0x00]), { mode: 0o600 });
  const hostConnection =
    `Host=127.0.0.1;Port=55440;Database=${database};Username=puntiro_restore_fixture;Password=fixture-only;Include Error Detail=false`;
  await writeFile(composeEnv, [
    'PUNTIRO_RESTORE_PROJECT=puntiro-restore-drill',
    'POSTGRES_DB=puntiro_restore_drill',
    'POSTGRES_USER=puntiro_restore_fixture',
    'POSTGRES_PASSWORD=fixture-only',
    'PUNTIRO_POSTGRES_PORT=55440',
    `PUNTIRO_CLOUD_UID=${process.getuid()}`,
    `PUNTIRO_CLOUD_GID=${process.getgid()}`,
    `PUNTIRO_CLOUD_RUNTIME_ENV_FILE=${runtimeEnv}`,
    `Puntiro__Security__DataProtectionCertificateHostPath=${certificate}`,
    `ConnectionStrings__Puntiro=${hostConnection}`,
    `PUNTIRO_TEST_POSTGRES=${hostConnection}`,
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
  await writeFile(path.join(bin, 'dotnet'), '#!/usr/bin/env node\nprocess.exit(0);\n');
  await chmod(path.join(bin, 'dotnet'), 0o755);
  return { bin, certificate, composeEnv, keyFile, keyRing, runtimeEnv };
}

function runValidator(fixture) {
  const { composeEnv, runtimeEnv } = fixture;
  return spawnSync(process.execPath, [
    validator,
    '--compose-env', composeEnv,
    '--runtime-env', runtimeEnv,
    '--restore-project', 'puntiro-restore-drill',
  ], {
    cwd: root,
    encoding: 'utf8',
    env: { ...process.env, PATH: `${fixture.bin}${path.delimiter}${process.env.PATH}` },
  });
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

test('rejects an empty restored certificate before Compose can create services', async t => {
  const fixture = await restoreFixture(t);
  await writeFile(fixture.certificate, Buffer.alloc(0), { mode: 0o600 });

  const result = runValidator(fixture);

  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /Data Protection certificate must contain nonempty PKCS#12 material/);
});

test('rejects missing key material before Compose can create or start restore services', async t => {
  const fixture = await restoreFixture(t);
  await rm(fixture.keyRing, { recursive: true });

  const result = runValidator(fixture);

  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /Data Protection key ring must exist/);
  assert.doesNotMatch(result.stderr, /fixture-only/);
});

test('rejects an empty private key-ring directory before Compose can create restore services', async t => {
  const fixture = await restoreFixture(t);
  await rm(fixture.keyFile);

  const result = runValidator(fixture);

  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /Data Protection key ring must contain a valid protected key/);
  assert.doesNotMatch(result.stderr, /fixture-only/);
});

test('rejects key-ring files that do not match the Data Protection key format', async t => {
  const fixture = await restoreFixture(t);
  await writeFile(fixture.keyFile, '<not-a-data-protection-key />', { mode: 0o600 });

  const result = runValidator(fixture);

  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /Data Protection key ring must contain a valid protected key/);
});

test('rejects a structurally plausible but unprotected Data Protection key', async t => {
  const fixture = await restoreFixture(t);
  const contents = await readFile(fixture.keyFile, 'utf8');
  await writeFile(
    fixture.keyFile,
    contents.replace(
      '<encryptedSecret decryptorType="fixture"><value>fixture</value></encryptedSecret>',
      '<descriptor />',
    ),
    { mode: 0o600 },
  );

  const result = runValidator(fixture);

  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /Data Protection key ring must contain a valid protected key/);
});

test('rejects a symlinked restored Data Protection key file', async t => {
  const fixture = await restoreFixture(t);
  const target = `${fixture.keyFile}.target`;
  const contents = await readFile(fixture.keyFile);
  await writeFile(target, contents, { mode: 0o600 });
  await rm(fixture.keyFile);
  await symlink(target, fixture.keyFile);

  const result = runValidator(fixture);

  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /Data Protection key file must be a regular file/);
});

test('rejects host tools that target a different database port or credential set', async t => {
  const mutations = [
    ['Port=55440', 'Port=5432'],
    ['Database=puntiro_restore_drill', 'Database=puntiro_normal'],
    ['Username=puntiro_restore_fixture', 'Username=other_restore_user'],
    ['Password=fixture-only', 'Password=other-review-probe'],
  ];

  for (const [from, to] of mutations) {
    const fixture = await restoreFixture(t);
    const contents = await readFile(fixture.composeEnv, 'utf8');
    await writeFile(
      fixture.composeEnv,
      contents.replace(`ConnectionStrings__Puntiro=${contents.match(/^ConnectionStrings__Puntiro=(.*)$/m)?.[1]}`, `ConnectionStrings__Puntiro=${contents.match(/^ConnectionStrings__Puntiro=(.*)$/m)?.[1].replace(from, to)}`),
    );
    await chmod(fixture.composeEnv, 0o600);

    const result = runValidator(fixture);
    assert.notEqual(result.status, 0, `${from} -> ${to}`);
    assert.match(result.stderr, /host-tool connections must target the same isolated restore database and credentials/);
    assert.doesNotMatch(result.stderr, /fixture-only|other-review-probe/);
  }
});

test('validates PUNTIRO_TEST_POSTGRES independently from the EF host connection', async t => {
  const fixture = await restoreFixture(t);
  const contents = await readFile(fixture.composeEnv, 'utf8');
  await writeFile(
    fixture.composeEnv,
    contents.replace(
      /^PUNTIRO_TEST_POSTGRES=.*$/m,
      'PUNTIRO_TEST_POSTGRES=Host=127.0.0.1;Port=55440;Database=puntiro_normal;Username=puntiro_restore_fixture;Password=other-review-probe',
    ),
  );
  await chmod(fixture.composeEnv, 0o600);

  const result = runValidator(fixture);

  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /host-tool connections must target the same isolated restore database and credentials/);
  assert.doesNotMatch(result.stderr, /other-review-probe|fixture-only/);
});

test('rejects a default restore loopback port and a non-loopback host connection', async t => {
  const fixture = await restoreFixture(t);
  let contents = await readFile(fixture.composeEnv, 'utf8');
  contents = contents.replace('PUNTIRO_POSTGRES_PORT=55440', 'PUNTIRO_POSTGRES_PORT=5432');
  contents = contents.replaceAll('Host=127.0.0.1;Port=55440', 'Host=database.internal;Port=5432');
  await writeFile(fixture.composeEnv, contents);
  await chmod(fixture.composeEnv, 0o600);

  const result = runValidator(fixture);

  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /restore host-tool connections must use the distinct documented loopback port/);
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

test('rejects a missing current retained HMAC version before creating restore services', async t => {
  const fixture = await restoreFixture(t);
  const contents = await readFile(fixture.runtimeEnv, 'utf8');
  await writeFile(
    fixture.runtimeEnv,
    contents.replace(/^Puntiro__Security__IntegrationHmac__Keys__v1=.*\n/m, ''),
  );
  await chmod(fixture.runtimeEnv, 0o600);

  const result = runValidator(fixture);

  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /Integration HMAC current key is missing from retained versions/);
  assert.doesNotMatch(result.stderr, /fixture-only/);
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
