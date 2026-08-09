import path from 'node:path';
import { lstat } from 'node:fs/promises';
import { loadCloudEnvFiles } from './cloud-env.mjs';

function argumentsFrom(argv) {
  const result = {};
  for (let index = 0; index < argv.length; index += 2) {
    const name = argv[index];
    const value = argv[index + 1];
    if (!value || !['--compose-env', '--runtime-env', '--restore-project'].includes(name)) {
      throw new Error(
        'Usage: validate-cloud-runtime-env.mjs --compose-env <path> --runtime-env <path> --restore-project <name>',
      );
    }
    result[name] = value;
  }
  if (!result['--compose-env'] || !result['--runtime-env'] || !result['--restore-project']) {
    throw new Error(
      'Usage: validate-cloud-runtime-env.mjs --compose-env <path> --runtime-env <path> --restore-project <name>',
    );
  }
  return {
    composeEnvPath: result['--compose-env'],
    runtimeEnvPath: result['--runtime-env'],
    restoreProject: result['--restore-project'],
  };
}

function required(values, name) {
  const value = values.get(name);
  if (value === undefined || value.trim().length === 0) {
    throw new Error(`required Cloud environment variable is missing: ${name}`);
  }
  return value;
}

function parseConnectionString(value) {
  const fields = new Map();
  for (const segment of value.split(';')) {
    if (segment.length === 0) continue;
    const separator = segment.indexOf('=');
    if (separator <= 0) throw new Error('runtime connection string format is invalid');
    fields.set(segment.slice(0, separator).trim().toLowerCase(), segment.slice(separator + 1));
  }
  return fields;
}

function isCanonicalHmac(value) {
  let decoded;
  try {
    decoded = Buffer.from(value, 'base64');
  } catch {
    return false;
  }
  return decoded.length === 32 && decoded.toString('base64') === value;
}

function validateHmac(runtime, purpose) {
  const prefix = `Puntiro__Security__${purpose}Hmac`;
  const version = required(runtime, `${prefix}__CurrentVersion`);
  const keyPrefix = `${prefix}__Keys__`;
  const retainedKeys = [...runtime.entries()].filter(([name]) => name.startsWith(keyPrefix));
  if (retainedKeys.length === 0 || !retainedKeys.some(([name]) => name === `${keyPrefix}${version}`)) {
    throw new Error(`${purpose} HMAC current key is missing from retained versions`);
  }
  if (retainedKeys.some(([, value]) => !isCanonicalHmac(value))) {
    throw new Error(`${purpose} HMAC retained key format is invalid`);
  }
  return retainedKeys.map(([, value]) => value);
}

function validateIndependentHmacKeys(...sets) {
  for (let left = 0; left < sets.length; left += 1) {
    for (let right = left + 1; right < sets.length; right += 1) {
      if (sets[left].some(value => sets[right].includes(value))) {
        throw new Error('HMAC key material must be distinct across purposes');
      }
    }
  }
}

async function requirePrivatePath(value, kind, message) {
  if (!path.isAbsolute(value)) throw new Error(message);
  let information;
  try {
    information = await lstat(value);
  } catch {
    throw new Error(message);
  }
  const expectedKind = kind === 'directory' ? information.isDirectory() : information.isFile();
  if (!expectedKind || information.isSymbolicLink() || (information.mode & 0o077) !== 0) {
    throw new Error(message);
  }
}

async function main() {
  const { composeEnvPath, runtimeEnvPath, restoreProject } = argumentsFrom(process.argv.slice(2));
  const compose = await loadCloudEnvFiles([composeEnvPath]);
  const runtime = await loadCloudEnvFiles([runtimeEnvPath]);
  if (required(compose, 'PUNTIRO_RESTORE_PROJECT') !== restoreProject) {
    throw new Error('restore project marker must equal the requested isolated project');
  }
  const database = required(compose, 'POSTGRES_DB');
  if (!/^puntiro_restore_[a-z0-9_]+$/.test(database)) {
    throw new Error('isolated restore database name must use the puntiro_restore_ prefix');
  }
  if (path.resolve(required(compose, 'PUNTIRO_CLOUD_RUNTIME_ENV_FILE')) !==
      path.resolve(runtimeEnvPath)) {
    throw new Error('Compose runtime env path must equal the validated restore runtime env file');
  }
  required(compose, 'POSTGRES_USER');
  required(compose, 'POSTGRES_PASSWORD');
  const certificateHostPath = required(
    compose,
    'Puntiro__Security__DataProtectionCertificateHostPath',
  );

  const connection = parseConnectionString(required(runtime, 'ConnectionStrings__Puntiro'));
  if (connection.get('host') !== 'postgres') {
    throw new Error('runtime connection host must be the isolated Compose postgres service');
  }
  if (connection.get('database') !== database) {
    throw new Error('runtime connection database must equal the isolated restore database');
  }
  required(runtime, 'Puntiro__Admin__AllowedOrigin');
  const keyRingPath = required(runtime, 'Puntiro__Security__DataProtectionKeysPath');
  const certificateRuntimePath = required(
    runtime,
    'Puntiro__Security__DataProtectionCertificatePath',
  );
  if (path.resolve(certificateRuntimePath) !== path.resolve(certificateHostPath)) {
    throw new Error('Data Protection certificate path must equal the validated host certificate path');
  }
  await requirePrivatePath(
    keyRingPath,
    'directory',
    'Data Protection key ring must be an existing private directory',
  );
  await requirePrivatePath(
    certificateHostPath,
    'file',
    'Data Protection certificate must be an existing private file',
  );
  required(runtime, 'Puntiro__Security__DataProtectionCertificatePassword');
  validateIndependentHmacKeys(
    validateHmac(runtime, 'Session'),
    validateHmac(runtime, 'Recovery'),
    validateHmac(runtime, 'Integration'),
  );

  console.log('Cloud restore environment contract passed.');
}

main().catch(error => {
  console.error(error instanceof Error ? error.message : 'Cloud runtime environment is invalid.');
  process.exitCode = 1;
});
