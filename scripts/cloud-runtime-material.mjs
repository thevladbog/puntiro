import { lstat, readFile, readdir } from 'node:fs/promises';
import path from 'node:path';

function modeOf(metadata) {
  return metadata.mode & 0o777;
}

function matchesOwner(metadata, uid, gid) {
  return metadata.uid === uid && metadata.gid === gid;
}

function requiredInteger(values, name) {
  const raw = values.get(name);
  if (!raw || !/^[1-9][0-9]{0,9}$/.test(raw)) {
    throw new Error(`required Cloud service identity is invalid: ${name}`);
  }
  const value = Number(raw);
  if (!Number.isSafeInteger(value) || value > 2_147_483_647) {
    throw new Error(`required Cloud service identity is invalid: ${name}`);
  }
  return value;
}

async function privateMetadata(target, kind, label, exactMode) {
  if (!path.isAbsolute(target)) throw new Error(`${label} must use an absolute path`);
  let metadata;
  try {
    metadata = await lstat(target);
  } catch {
    throw new Error(`${label} must exist`);
  }
  const expectedKind = kind === 'directory' ? metadata.isDirectory() : metadata.isFile();
  if (!expectedKind || metadata.isSymbolicLink()) {
    throw new Error(`${label} must be a regular ${kind}`);
  }
  if (modeOf(metadata) !== exactMode) {
    throw new Error(`${label} must be mode ${exactMode.toString(8).padStart(4, '0')}`);
  }
  return metadata;
}

function isExpectedDataProtectionKey(content, expectedId) {
  const escapedId = expectedId.replaceAll('-', '\\-');
  const root = new RegExp(`<key\\s+[^>]*id=["']${escapedId}["'][^>]*version=["']1["'][^>]*>`, 'i');
  return root.test(content) &&
    /<creationDate>[^<]+<\/creationDate>/i.test(content) &&
    /<activationDate>[^<]+<\/activationDate>/i.test(content) &&
    /<expirationDate>[^<]+<\/expirationDate>/i.test(content) &&
    /<encryptedSecret\b[^>]*decryptorType=["'][^"']+["']/i.test(content) &&
    /<\/key>\s*$/i.test(content);
}

async function validateDataProtectionRing(ringPath, uid, gid) {
  const ringMetadata = await privateMetadata(
    ringPath,
    'directory',
    'Data Protection key ring',
    0o700,
  );
  const entries = await readdir(ringPath);
  const candidates = entries.filter(name =>
    /^key-[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\.xml$/i.test(name));
  if (candidates.length === 0) {
    throw new Error('Data Protection key ring must contain a valid protected key');
  }

  const keyMetadata = [];
  for (const name of candidates) {
    const target = path.join(ringPath, name);
    const metadata = await privateMetadata(
      target,
      'file',
      'Data Protection key file',
      0o600,
    );
    const content = await readFile(target, 'utf8');
    const expectedId = name.slice(4, -4);
    if (content.length === 0 || content.length > 1024 * 1024 ||
        !isExpectedDataProtectionKey(content, expectedId)) {
      throw new Error('Data Protection key ring must contain a valid protected key');
    }
    keyMetadata.push(metadata);
  }

  if (!matchesOwner(ringMetadata, uid, gid) ||
      keyMetadata.some(metadata => !matchesOwner(metadata, uid, gid))) {
    throw new Error(
      'runtime key ring and certificate must be owned by the configured Cloud uid and gid',
    );
  }
}

async function validateCertificate(certificatePath, uid, gid) {
  const metadata = await privateMetadata(
    certificatePath,
    'file',
    'Data Protection certificate',
    0o600,
  );
  const content = await readFile(certificatePath);
  if (content.length < 4 || content.length > 16 * 1024 * 1024 || content[0] !== 0x30) {
    throw new Error('Data Protection certificate must contain nonempty PKCS#12 material');
  }
  if (!matchesOwner(metadata, uid, gid)) {
    throw new Error(
      'runtime key ring and certificate must be owned by the configured Cloud uid and gid',
    );
  }
}

export async function validateCloudRuntimeMaterial({
  compose,
  composeEnvPath,
  runtime,
  runtimeEnvPath,
}) {
  const configuredRuntimeEnv = compose.get('PUNTIRO_CLOUD_RUNTIME_ENV_FILE');
  const resolvedConfiguredRuntimeEnv = configuredRuntimeEnv && path.resolve(
    path.dirname(path.resolve(composeEnvPath)),
    configuredRuntimeEnv,
  );
  if (!resolvedConfiguredRuntimeEnv ||
      resolvedConfiguredRuntimeEnv !== path.resolve(runtimeEnvPath)) {
    throw new Error('Compose runtime env path must equal the validated runtime env file');
  }

  const runtimeEnvMetadata = await privateMetadata(
    path.resolve(runtimeEnvPath),
    'file',
    'Cloud runtime env file',
    0o600,
  );
  const uid = requiredInteger(compose, 'PUNTIRO_CLOUD_UID');
  const gid = requiredInteger(compose, 'PUNTIRO_CLOUD_GID');
  const ringPath = runtime.get('Puntiro__Security__DataProtectionKeysPath');
  const certificateRuntimePath = runtime.get(
    'Puntiro__Security__DataProtectionCertificatePath',
  );
  const certificateHostPath = compose.get(
    'Puntiro__Security__DataProtectionCertificateHostPath',
  );
  if (!ringPath) throw new Error('Data Protection key ring path is required');
  if (!certificateRuntimePath || !certificateHostPath ||
      path.resolve(certificateRuntimePath) !== path.resolve(certificateHostPath)) {
    throw new Error('Data Protection certificate path must equal the validated host certificate path');
  }

  await validateDataProtectionRing(ringPath, uid, gid);
  await validateCertificate(certificateHostPath, uid, gid);

  return { gid, runtimeEnvMetadata, uid };
}
