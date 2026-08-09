import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { chmod, mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const wrapper = path.join(root, 'scripts', 'cloud-compose.mjs');

async function wrapperFixture(t, {
  dotnetExit = 0,
  omitNormalizedBindPolicy = false,
  useComposeDefaults = false,
} = {}) {
  const directory = await mkdtemp(path.join(os.tmpdir(), 'puntiro-cloud-wrapper-'));
  t.after(() => rm(directory, { recursive: true, force: true }));
  const bin = path.join(directory, 'bin');
  const log = path.join(directory, 'calls.jsonl');
  const composeEnv = path.join(directory, 'compose.env');
  const runtimeEnv = path.join(directory, 'runtime.env');
  const ring = path.join(directory, 'ring');
  const certificate = path.join(directory, 'certificate.pfx');
  const keyId = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee';
  await mkdir(bin);
  await mkdir(ring, { mode: 0o700 });
  await writeFile(
    path.join(ring, `key-${keyId}.xml`),
    `<key id="${keyId}" version="1"><creationDate>2026-08-09T00:00:00Z</creationDate><activationDate>2026-08-09T00:00:00Z</activationDate><expirationDate>2026-11-09T00:00:00Z</expirationDate><descriptor><encryptedSecret decryptorType="fixture"><value>fixture</value></encryptedSecret></descriptor></key>`,
    { mode: 0o600 },
  );
  await writeFile(certificate, Buffer.from([0x30, 0x82, 0x00, 0x01, 0x00]), { mode: 0o600 });
  const hostConnection = 'Host=127.0.0.1;Port=55440;Database=puntiro_restore_drill;Username=puntiro_restore_fixture;Password=fixture-only;Include Error Detail=false';
  const containerConnection = 'Host=postgres;Port=5432;Database=puntiro_restore_drill;Username=puntiro_restore_fixture;Password=fixture-only;Include Error Detail=false';
  await writeFile(composeEnv, [
    'PUNTIRO_RESTORE_PROJECT=puntiro-restore-drill',
    useComposeDefaults ? undefined : 'PUNTIRO_POSTGRES_PORT=55440',
    'POSTGRES_DB=puntiro_restore_drill',
    'POSTGRES_USER=puntiro_restore_fixture',
    'POSTGRES_PASSWORD=fixture-only',
    useComposeDefaults ? undefined : 'PUNTIRO_CLOUD_IMAGE=puntiro-cloud@sha256:fixture',
    `PUNTIRO_CLOUD_RUNTIME_ENV_FILE=${runtimeEnv}`,
    `PUNTIRO_CLOUD_UID=${process.getuid()}`,
    `PUNTIRO_CLOUD_GID=${process.getgid()}`,
    `Puntiro__Security__DataProtectionCertificateHostPath=${certificate}`,
    `ConnectionStrings__Puntiro=${hostConnection}`,
    `PUNTIRO_TEST_POSTGRES=${hostConnection}`,
    '',
  ].filter(value => value !== undefined).join('\n'));
  const runtime = {
    ConnectionStrings__Puntiro: containerConnection,
    Puntiro__Admin__AllowedOrigin: 'https://restore.invalid/',
    Puntiro__Proxy__Enabled: 'false',
    Puntiro__Security__DataProtectionKeysPath: ring,
    Puntiro__Security__DataProtectionCertificatePath: certificate,
    Puntiro__Security__DataProtectionCertificatePassword: 'fixture-only',
    Puntiro__Security__SessionHmac__CurrentVersion: 'v1',
    Puntiro__Security__SessionHmac__Keys__v1: Buffer.alloc(32, 0x61).toString('base64'),
    Puntiro__Security__RecoveryHmac__CurrentVersion: 'v1',
    Puntiro__Security__RecoveryHmac__Keys__v1: Buffer.alloc(32, 0x62).toString('base64'),
    Puntiro__Security__IntegrationHmac__CurrentVersion: 'v1',
    Puntiro__Security__IntegrationHmac__Keys__v1: Buffer.alloc(32, 0x63).toString('base64'),
  };
  await writeFile(runtimeEnv, `${Object.entries(runtime).map(([name, value]) => `${name}=${value}`).join('\n')}\n`);
  await chmod(composeEnv, 0o600);
  await chmod(runtimeEnv, 0o600);

  const resolved = {
    services: {
      postgres: {
        environment: {
          POSTGRES_DB: 'puntiro_restore_drill',
          POSTGRES_USER: 'puntiro_restore_fixture',
          POSTGRES_PASSWORD: 'fixture-only',
        },
        ports: [{ host_ip: '127.0.0.1', published: useComposeDefaults ? '5432' : '55440', target: 5432 }],
      },
      cloud: {
        image: useComposeDefaults ? 'puntiro-cloud:local' : 'puntiro-cloud@sha256:fixture',
        user: `${process.getuid()}:${process.getgid()}`,
        environment: {
          ...runtime,
          Puntiro__Security__DataProtectionKeysPath: '/var/lib/puntiro/data-protection-keys',
          Puntiro__Security__DataProtectionCertificatePath: '/run/puntiro-secrets/data-protection.pfx',
          ASPNETCORE_ENVIRONMENT: 'Production',
          ASPNETCORE_URLS: 'http://0.0.0.0:8080',
        },
        volumes: [
          {
            type: 'bind',
            source: ring,
            target: '/var/lib/puntiro/data-protection-keys',
            ...(omitNormalizedBindPolicy ? {} : { bind: { create_host_path: false } }),
          },
          {
            type: 'bind',
            source: certificate,
            target: '/run/puntiro-secrets/data-protection.pfx',
            read_only: true,
            ...(omitNormalizedBindPolicy ? {} : { bind: { create_host_path: false } }),
          },
        ],
      },
    },
  };
  const append = `await import('node:fs/promises').then(fs => fs.appendFile(${JSON.stringify(log)}, JSON.stringify({phase, targetedPresent: ['PUNTIRO_CLOUD_UID','PUNTIRO_CLOUD_RUNTIME_ENV_FILE','Puntiro__Security__DataProtectionKeysPath'].some(name => Object.hasOwn(process.env, name))}) + '\\n'));`;
  await writeFile(path.join(bin, 'docker'), `#!/usr/bin/env node
const phase = process.argv.includes('config') ? 'docker-config' : 'docker-operation';
${append}
if (phase === 'docker-config') process.stdout.write(${JSON.stringify(JSON.stringify(resolved))});
`);
  await writeFile(path.join(bin, 'dotnet'), `#!/usr/bin/env node
const phase = 'dotnet-preflight';
await import('node:fs/promises').then(fs => fs.appendFile(${JSON.stringify(log)}, JSON.stringify({phase, exactInput: process.env.PUNTIRO_PREFLIGHT_CONTAINER_CONNECTION === ${JSON.stringify(containerConnection)}, hostPort: process.env.PUNTIRO_PREFLIGHT_HOST_PORT}) + '\\n'));
process.exit(${dotnetExit});
`);
  await chmod(path.join(bin, 'docker'), 0o755);
  await chmod(path.join(bin, 'dotnet'), 0o755);
  return { bin, composeEnv, log, runtimeEnv };
}

function runWrapper(fixture, mode, operation) {
  const args = [
    wrapper,
    '--mode', mode,
    '--compose-env', fixture.composeEnv,
    '--runtime-env', fixture.runtimeEnv,
    '--operation', operation,
  ];
  if (mode === 'restore') args.push('--restore-project', 'puntiro-restore-drill');
  return spawnSync(process.execPath, args, {
    cwd: root,
    encoding: 'utf8',
    env: {
      ...process.env,
      PATH: `${fixture.bin}${path.delimiter}${process.env.PATH}`,
      PUNTIRO_CLOUD_UID: '99999',
      PUNTIRO_CLOUD_RUNTIME_ENV_FILE: '/ambient/override.env',
      Puntiro__Security__DataProtectionKeysPath: '/ambient/ring',
    },
  });
}

test('sanitizes ambient Compose interpolation and runs preflight config verification then Cloud operation', async t => {
  const fixture = await wrapperFixture(t);

  const result = runWrapper(fixture, 'normal', 'up-cloud');

  assert.equal(result.status, 0, result.stderr);
  const calls = (await readFile(fixture.log, 'utf8')).trim().split('\n').map(JSON.parse);
  assert.deepEqual(calls.map(call => call.phase), [
    'dotnet-preflight',
    'docker-config',
    'docker-operation',
  ]);
  assert.equal(calls[0].exactInput, true);
  assert.equal(calls[1].targetedPresent, false);
  assert.equal(calls[2].targetedPresent, false);
});

test('does not resolve or operate Compose when the compiled preflight fails', async t => {
  const fixture = await wrapperFixture(t, { dotnetExit: 31 });

  const result = runWrapper(fixture, 'normal', 'up-cloud');

  assert.notEqual(result.status, 0);
  const calls = (await readFile(fixture.log, 'utf8')).trim().split('\n').map(JSON.parse);
  assert.deepEqual(calls.map(call => call.phase), ['dotnet-preflight']);
  assert.doesNotMatch(result.stderr, /fixture-only|Host=/);
});

test('verifies the documented local image and PostgreSQL port defaults', async t => {
  const fixture = await wrapperFixture(t, { useComposeDefaults: true });

  const result = runWrapper(fixture, 'normal', 'up-cloud');

  assert.equal(result.status, 0, result.stderr);
  const calls = (await readFile(fixture.log, 'utf8')).trim().split('\n').map(JSON.parse);
  assert.deepEqual(calls.map(call => call.phase), [
    'dotnet-preflight',
    'docker-config',
    'docker-operation',
  ]);
  assert.equal(calls[0].hostPort, '5432');
});

test('accepts Compose implementations that omit explicit false bind policy from normalized JSON', async t => {
  const fixture = await wrapperFixture(t, { omitNormalizedBindPolicy: true });

  const result = runWrapper(fixture, 'normal', 'up-cloud');

  assert.equal(result.status, 0, result.stderr);
});

test('uses the same checked wrapper before starting isolated restore PostgreSQL', async t => {
  const fixture = await wrapperFixture(t);

  const result = runWrapper(fixture, 'restore', 'up-postgres');

  assert.equal(result.status, 0, result.stderr);
  const calls = (await readFile(fixture.log, 'utf8')).trim().split('\n').map(JSON.parse);
  assert.deepEqual(calls.map(call => call.phase), [
    'dotnet-preflight',
    'docker-config',
    'docker-operation',
  ]);
});
