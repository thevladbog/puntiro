import { loadCloudEnvFiles } from './cloud-env.mjs';
import { validateCloudRuntimeMaterial } from './cloud-runtime-material.mjs';

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
    const name = segment.slice(0, separator).trim().toLowerCase();
    if (fields.has(name)) throw new Error('runtime connection string format is invalid');
    fields.set(name, segment.slice(separator + 1));
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

function assertConnectionTarget(connection, expected) {
  return connection.get('host') === expected.host &&
    connection.get('port') === expected.port &&
    connection.get('database') === expected.database &&
    connection.get('username') === expected.username &&
    connection.get('password') === expected.password;
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
  const username = required(compose, 'POSTGRES_USER');
  const password = required(compose, 'POSTGRES_PASSWORD');
  const restorePort = required(compose, 'PUNTIRO_POSTGRES_PORT');
  if (!/^[1-9][0-9]{3,4}$/.test(restorePort) || Number(restorePort) > 65535 ||
      restorePort === '5432') {
    throw new Error(
      'restore host-tool connections must use the distinct documented loopback port',
    );
  }

  const connection = parseConnectionString(required(runtime, 'ConnectionStrings__Puntiro'));
  if (connection.get('host') !== 'postgres' || connection.get('port') !== '5432') {
    throw new Error('runtime connection host must be the isolated Compose postgres service');
  }
  if (connection.get('database') !== database) {
    throw new Error('runtime connection database must equal the isolated restore database');
  }
  if (connection.get('username') !== username || connection.get('password') !== password) {
    throw new Error('runtime connection credentials must equal the isolated restore database credentials');
  }

  const expectedHost = {
    host: '127.0.0.1',
    port: restorePort,
    database,
    username,
    password,
  };
  const hostConnections = [
    parseConnectionString(required(compose, 'ConnectionStrings__Puntiro')),
    parseConnectionString(required(compose, 'PUNTIRO_TEST_POSTGRES')),
  ];
  if (hostConnections.some(hostConnection =>
    !assertConnectionTarget(hostConnection, expectedHost))) {
    throw new Error(
      'host-tool connections must target the same isolated restore database and credentials',
    );
  }

  required(runtime, 'Puntiro__Admin__AllowedOrigin');
  await validateCloudRuntimeMaterial({ compose, composeEnvPath, runtime, runtimeEnvPath });
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
