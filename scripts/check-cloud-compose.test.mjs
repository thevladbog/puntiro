import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { chmod, mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { verifyBindCreationPolicy } from './cloud-compose.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const composePath = path.join(root, 'infra', 'compose', 'cloud-development.yml');

test('source policy rejects bind mounts that can auto-create secret paths', async () => {
  const source = await readFile(composePath, 'utf8');

  assert.throws(
    () => verifyBindCreationPolicy(source.replace('create_host_path: false', 'create_host_path: true')),
    /explicitly disable host-path creation/,
  );
});

test('database-only configuration needs no Cloud runtime env file and keeps Cloud opt-in', async t => {
  const directory = await mkdtemp(path.join(os.tmpdir(), 'puntiro-postgres-compose-'));
  t.after(() => rm(directory, { recursive: true, force: true }));
  const composeEnvPath = path.join(directory, 'compose.env');
  await writeFile(composeEnvPath, [
    'POSTGRES_DB=puntiro_compose_fixture',
    'POSTGRES_USER=puntiro_compose_fixture',
    'POSTGRES_PASSWORD=fixture-only',
    'PUNTIRO_POSTGRES_PORT=55439',
    `PUNTIRO_CLOUD_UID=${process.getuid()}`,
    `PUNTIRO_CLOUD_GID=${process.getgid()}`,
    `PUNTIRO_CLOUD_RUNTIME_ENV_FILE=${path.join(directory, 'absent-runtime.env')}`,
    `Puntiro__Security__DataProtectionKeysPath=${path.join(directory, 'absent-ring')}`,
    `Puntiro__Security__DataProtectionCertificateHostPath=${path.join(directory, 'absent-certificate.pfx')}`,
    '',
  ].join('\n'));

  const result = spawnSync('docker', [
    'compose',
    '--env-file', composeEnvPath,
    '--file', composePath,
    'config',
    '--format', 'json',
  ], { cwd: root, encoding: 'utf8', env: process.env });

  assert.equal(result.status, 0, `docker compose config failed: ${result.stderr}`);
  const config = JSON.parse(result.stdout);
  assert.equal(config.services.postgres.image, 'postgres:17.10-bookworm');
  assert.equal(config.services.cloud, undefined);
  assert.equal(config.services.postgres.ports[0].host_ip, '127.0.0.1');
  assert.equal(config.services.postgres.volumes[0].source, 'puntiro-cloud-postgres');
});

async function resolvedCloud(t, runtimeOverrides = {}) {
  const directory = await mkdtemp(path.join(os.tmpdir(), 'puntiro-cloud-compose-'));
  t.after(() => rm(directory, { recursive: true, force: true }));
  const ringPath = path.join(directory, 'data-protection-keys');
  const certificatePath = path.join(directory, 'data-protection.pfx');
  const composeEnvPath = path.join(directory, 'compose.env');
  const runtimeEnvPath = path.join(directory, 'runtime.env');
  await mkdir(ringPath, { recursive: true });
  await writeFile(certificatePath, 'fixture-only');
  await writeFile(composeEnvPath, [
    'POSTGRES_DB=puntiro_compose_fixture',
    'POSTGRES_USER=puntiro_compose_fixture',
    'POSTGRES_PASSWORD=fixture-only',
    'PUNTIRO_POSTGRES_PORT=55439',
    'PUNTIRO_CLOUD_IMAGE=puntiro-cloud:fixture-only',
    `PUNTIRO_CLOUD_UID=${process.getuid()}`,
    `PUNTIRO_CLOUD_GID=${process.getgid()}`,
    `PUNTIRO_CLOUD_RUNTIME_ENV_FILE=${runtimeEnvPath}`,
    `Puntiro__Security__DataProtectionCertificateHostPath=${certificatePath}`,
    '',
  ].join('\n'));

  const runtime = {
    ConnectionStrings__Puntiro:
      'Host=postgres;Port=5432;Database=puntiro_compose_fixture;Username=puntiro_compose_fixture;Password=fixture-only',
    Puntiro__Admin__AllowedOrigin: 'https://fixture.invalid/',
    Puntiro__Proxy__Enabled: 'false',
    Puntiro__Security__DataProtectionKeysPath: ringPath,
    Puntiro__Security__DataProtectionCertificatePassword: 'fixture-only',
    Puntiro__Security__SessionHmac__CurrentVersion: 'v2',
    Puntiro__Security__SessionHmac__Keys__v0: 'fixture-session-v0',
    Puntiro__Security__SessionHmac__Keys__v2: 'fixture-session-v2',
    Puntiro__Security__RecoveryHmac__CurrentVersion: 'v1',
    Puntiro__Security__RecoveryHmac__Keys__v1: 'fixture-recovery-v1',
    Puntiro__Security__RecoveryHmac__Keys__v2: 'fixture-recovery-v2',
    Puntiro__Security__IntegrationHmac__CurrentVersion: 'v2',
    Puntiro__Security__IntegrationHmac__Keys__v0: 'fixture-integration-v0',
    Puntiro__Security__IntegrationHmac__Keys__v2: 'fixture-integration-v2',
    ...runtimeOverrides,
  };
  await writeFile(runtimeEnvPath, `${Object.entries(runtime)
    .filter(([, value]) => value !== undefined)
    .map(([name, value]) => `${name}=${value}`)
    .join('\n')}\n`);
  await chmod(composeEnvPath, 0o600);
  await chmod(runtimeEnvPath, 0o600);

  const result = spawnSync('docker', [
    'compose',
    '--env-file', composeEnvPath,
    '--env-file', runtimeEnvPath,
    '--file', composePath,
    '--profile', 'cloud-runtime',
    'config',
    '--format', 'json',
  ], {
    cwd: root,
    encoding: 'utf8',
    env: process.env,
  });
  assert.equal(result.status, 0, `docker compose config failed: ${result.stderr}`);
  return {
    cloud: JSON.parse(result.stdout).services.cloud,
    certificatePath,
    ringPath,
  };
}

test('disabled proxy omits every indexed proxy value from the Cloud environment', async t => {
  const { cloud } = await resolvedCloud(t);

  assert.equal(cloud.environment.Puntiro__Proxy__Enabled, 'false');
  assert.equal('Puntiro__Proxy__KnownProxies__0' in cloud.environment, false);
  assert.equal('Puntiro__Proxy__KnownNetworks__0' in cloud.environment, false);
});

test('proxy-only, network-only and combined allowlists contain no synthetic blank elements', async t => {
  const cases = [
    {
      values: {
        Puntiro__Proxy__Enabled: 'true',
        Puntiro__Proxy__KnownProxies__0: '10.30.0.10',
      },
      expected: { proxies: ['10.30.0.10'], networks: [] },
    },
    {
      values: {
        Puntiro__Proxy__Enabled: 'true',
        Puntiro__Proxy__KnownNetworks__0: '10.30.0.0/24',
      },
      expected: { proxies: [], networks: ['10.30.0.0/24'] },
    },
    {
      values: {
        Puntiro__Proxy__Enabled: 'true',
        Puntiro__Proxy__KnownProxies__0: '10.30.0.10',
        Puntiro__Proxy__KnownNetworks__0: '10.30.0.0/24',
      },
      expected: { proxies: ['10.30.0.10'], networks: ['10.30.0.0/24'] },
    },
  ];

  for (const item of cases) {
    const { cloud } = await resolvedCloud(t, {
      Puntiro__Proxy__KnownProxies__0: undefined,
      Puntiro__Proxy__KnownNetworks__0: undefined,
      ...item.values,
    });
    const proxies = Object.entries(cloud.environment)
      .filter(([name]) => name.startsWith('Puntiro__Proxy__KnownProxies__'))
      .map(([, value]) => value);
    const networks = Object.entries(cloud.environment)
      .filter(([name]) => name.startsWith('Puntiro__Proxy__KnownNetworks__'))
      .map(([, value]) => value);
    assert.deepEqual(proxies, item.expected.proxies);
    assert.deepEqual(networks, item.expected.networks);
  }
});

test('all retained versioned HMAC variables pass through without enumerating versions in Compose', async t => {
  const { cloud } = await resolvedCloud(t);

  assert.equal(cloud.environment.Puntiro__Security__SessionHmac__Keys__v0, 'fixture-session-v0');
  assert.equal(cloud.environment.Puntiro__Security__SessionHmac__Keys__v2, 'fixture-session-v2');
  assert.equal(cloud.environment.Puntiro__Security__RecoveryHmac__Keys__v1, 'fixture-recovery-v1');
  assert.equal(cloud.environment.Puntiro__Security__RecoveryHmac__Keys__v2, 'fixture-recovery-v2');
  assert.equal(cloud.environment.Puntiro__Security__IntegrationHmac__Keys__v0, 'fixture-integration-v0');
  assert.equal(cloud.environment.Puntiro__Security__IntegrationHmac__Keys__v2, 'fixture-integration-v2');
  assert.equal('POSTGRES_PASSWORD' in cloud.environment, false);
});

test('Cloud bind-mounts the same host Data Protection ring and certificate used by provisioning', async t => {
  const { certificatePath, cloud, ringPath } = await resolvedCloud(t);
  verifyBindCreationPolicy(await readFile(composePath, 'utf8'));
  const ring = cloud.volumes.find(volume =>
    volume.target === '/var/lib/puntiro/data-protection-keys');
  const certificate = cloud.volumes.find(volume =>
    volume.target === '/run/puntiro-secrets/data-protection.pfx');

  assert.equal(ring.type, 'bind');
  assert.equal(ring.source, ringPath);
  assert.equal(ring.target, '/var/lib/puntiro/data-protection-keys');
  assert.notEqual(ring.bind?.create_host_path, true);
  assert.equal(
    cloud.environment.Puntiro__Security__DataProtectionKeysPath,
    '/var/lib/puntiro/data-protection-keys',
  );
  assert.equal(certificate.type, 'bind');
  assert.equal(certificate.source, certificatePath);
  assert.equal(certificate.read_only, true);
  assert.notEqual(certificate.bind?.create_host_path, true);
  assert.equal(
    cloud.environment.Puntiro__Security__DataProtectionCertificatePath,
    '/run/puntiro-secrets/data-protection.pfx',
  );
  assert.equal(cloud.user, `${process.getuid()}:${process.getgid()}`);
});

test('Cloud profile requires the runtime env file instead of silently starting without secrets', async t => {
  const directory = await mkdtemp(path.join(os.tmpdir(), 'puntiro-cloud-compose-missing-'));
  t.after(() => rm(directory, { recursive: true, force: true }));
  const composeEnvPath = path.join(directory, 'compose.env');
  const missingRuntime = path.join(directory, 'missing-runtime.env');
  await writeFile(composeEnvPath, [
    'POSTGRES_DB=puntiro_compose_fixture',
    'POSTGRES_USER=puntiro_compose_fixture',
    'POSTGRES_PASSWORD=fixture-only',
    'PUNTIRO_POSTGRES_PORT=55439',
    `PUNTIRO_CLOUD_UID=${process.getuid()}`,
    `PUNTIRO_CLOUD_GID=${process.getgid()}`,
    `PUNTIRO_CLOUD_RUNTIME_ENV_FILE=${missingRuntime}`,
    `Puntiro__Security__DataProtectionKeysPath=${path.join(directory, 'missing-ring')}`,
    `Puntiro__Security__DataProtectionCertificateHostPath=${path.join(directory, 'missing-certificate.pfx')}`,
    '',
  ].join('\n'));

  const result = spawnSync('docker', [
    'compose',
    '--env-file', composeEnvPath,
    '--file', composePath,
    '--profile', 'cloud-runtime',
    'config',
    '--format', 'json',
  ], { cwd: root, encoding: 'utf8', env: process.env });

  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /missing-runtime\.env/);
});
