import { spawnSync } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { loadCloudEnvFiles } from './cloud-env.mjs';
import { sanitizedCloudEnvironment } from './cloud-runtime-material.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const composeFile = path.join(root, 'infra', 'compose', 'cloud-development.yml');

function argumentsFrom(argv) {
  const values = new Map();
  for (let index = 0; index < argv.length; index += 2) {
    const name = argv[index];
    const value = argv[index + 1];
    if (!value || ![
      '--mode',
      '--compose-env',
      '--runtime-env',
      '--restore-project',
      '--operation',
    ].includes(name) || values.has(name)) {
      throw new Error('Cloud Compose wrapper arguments are invalid.');
    }
    values.set(name, value);
  }
  const mode = values.get('--mode');
  const operation = values.get('--operation');
  const composeEnvPath = values.get('--compose-env');
  const runtimeEnvPath = values.get('--runtime-env');
  const restoreProject = values.get('--restore-project');
  if (!['normal', 'restore'].includes(mode) || !composeEnvPath || !runtimeEnvPath ||
      !['up-cloud', 'create-cloud', 'start-cloud', 'up-postgres'].includes(operation) ||
      mode === 'normal' && (restoreProject || operation === 'up-postgres') ||
      mode === 'restore' && !restoreProject) {
    throw new Error('Cloud Compose wrapper arguments are invalid.');
  }
  return { composeEnvPath, mode, operation, restoreProject, runtimeEnvPath };
}

function runChecked(command, args, environment, failure) {
  const result = spawnSync(command, args, {
    cwd: root,
    encoding: 'utf8',
    env: environment,
    maxBuffer: 16 * 1024 * 1024,
  });
  if (result.status !== 0) throw new Error(failure);
  return result.stdout;
}

function assertEqual(actual, expected) {
  if (String(actual) !== String(expected)) throw new Error('Resolved Compose contract differs from validated input.');
}

function verifyResolvedCompose(resolved, compose, runtime) {
  const cloud = resolved?.services?.cloud;
  const postgres = resolved?.services?.postgres;
  if (!cloud || !postgres) throw new Error('Resolved Compose contract differs from validated input.');
  assertEqual(cloud.image, compose.get('PUNTIRO_CLOUD_IMAGE') || 'puntiro-cloud:local');
  assertEqual(
    cloud.user,
    `${compose.get('PUNTIRO_CLOUD_UID')}:${compose.get('PUNTIRO_CLOUD_GID')}`,
  );
  for (const [name, value] of runtime) {
    const expected = name === 'Puntiro__Security__DataProtectionKeysPath'
      ? '/var/lib/puntiro/data-protection-keys'
      : name === 'Puntiro__Security__DataProtectionCertificatePath'
        ? '/run/puntiro-secrets/data-protection.pfx'
        : value;
    assertEqual(cloud.environment?.[name], expected);
  }
  const dynamicNames = Object.keys(cloud.environment ?? {}).filter(name =>
    /^(?:ConnectionStrings__|Puntiro__)/.test(name));
  if (dynamicNames.some(name => !runtime.has(name))) {
    throw new Error('Resolved Compose contract differs from validated input.');
  }

  assertEqual(postgres.environment?.POSTGRES_DB, compose.get('POSTGRES_DB'));
  assertEqual(postgres.environment?.POSTGRES_USER, compose.get('POSTGRES_USER'));
  assertEqual(postgres.environment?.POSTGRES_PASSWORD, compose.get('POSTGRES_PASSWORD'));
  const port = postgres.ports?.find(item => Number(item.target) === 5432);
  assertEqual(port?.host_ip, '127.0.0.1');
  assertEqual(port?.published, compose.get('PUNTIRO_POSTGRES_PORT') || '5432');

  const ring = cloud.volumes?.find(item =>
    item.target === '/var/lib/puntiro/data-protection-keys');
  const certificate = cloud.volumes?.find(item =>
    item.target === '/run/puntiro-secrets/data-protection.pfx');
  assertEqual(ring?.type, 'bind');
  assertEqual(ring?.source, runtime.get('Puntiro__Security__DataProtectionKeysPath'));
  assertEqual(ring?.bind?.create_host_path, false);
  assertEqual(certificate?.type, 'bind');
  assertEqual(
    certificate?.source,
    compose.get('Puntiro__Security__DataProtectionCertificateHostPath'),
  );
  assertEqual(certificate?.read_only, true);
  assertEqual(certificate?.bind?.create_host_path, false);
}

export async function runCloudCompose(argv) {
  const { composeEnvPath, mode, operation, restoreProject, runtimeEnvPath } =
    argumentsFrom(argv);
  const compose = await loadCloudEnvFiles([composeEnvPath]);
  const runtime = await loadCloudEnvFiles([runtimeEnvPath]);
  const environment = sanitizedCloudEnvironment();
  const preflight = mode === 'restore'
    ? [
        path.join(root, 'scripts', 'validate-cloud-runtime-env.mjs'),
        '--compose-env', composeEnvPath,
        '--runtime-env', runtimeEnvPath,
        '--restore-project', restoreProject,
      ]
    : [
        path.join(root, 'scripts', 'preflight-cloud-runtime.mjs'),
        '--compose-env', composeEnvPath,
        '--runtime-env', runtimeEnvPath,
      ];
  runChecked(process.execPath, preflight, environment, 'Cloud preflight failed.');

  const base = ['compose'];
  if (mode === 'restore') base.push('-p', restoreProject);
  base.push(
    '--env-file', composeEnvPath,
    '--env-file', runtimeEnvPath,
    '--file', composeFile,
  );
  const resolvedText = runChecked(
    'docker',
    [...base, '--profile', 'cloud-runtime', 'config', '--format', 'json'],
    environment,
    'Cloud Compose resolution failed.',
  );
  let resolved;
  try {
    resolved = JSON.parse(resolvedText);
  } catch {
    throw new Error('Cloud Compose resolution failed.');
  }
  verifyResolvedCompose(resolved, compose, runtime);

  const operationArgs = {
    'up-cloud': ['--profile', 'cloud-runtime', 'up', '-d', 'cloud'],
    'create-cloud': ['--profile', 'cloud-runtime', 'create', 'cloud'],
    'start-cloud': ['--profile', 'cloud-runtime', 'start', 'cloud'],
    'up-postgres': ['up', '-d', 'postgres'],
  }[operation];
  runChecked(
    'docker',
    [...base, ...operationArgs],
    environment,
    'Cloud Compose operation failed.',
  );
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? '').href) {
  runCloudCompose(process.argv.slice(2)).then(() => {
    console.log('Cloud Compose operation completed.');
  }).catch(error => {
    console.error(error instanceof Error ? error.message : 'Cloud Compose operation failed.');
    process.exitCode = 1;
  });
}
