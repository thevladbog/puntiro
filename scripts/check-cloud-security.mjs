import { access, readFile, readdir } from 'node:fs/promises';
import { fileURLToPath, pathToFileURL } from 'node:url';
import path from 'node:path';

const requiredArtifacts = [
  'apps/cloud/Program.cs',
  'apps/cloud/appsettings.json',
  'apps/cloud/Auth/AdminSessionAuthenticationHandler.cs',
  'apps/cloud/Endpoints/AdminAuthEndpoints.cs',
  'apps/cloud/Configuration/CloudServiceCollectionExtensions.cs',
  'infra/compose/cloud-development.yml',
  'infra/compose/.env.cloud.example',
  'docs/adr/0002-global-identity-and-credentials.md',
  'docs/modules/identity.md',
  'docs/modules/tenancy.md',
  'docs/modules/integrations.md',
  'docs/runbooks/cloud-development.md',
  'docs/runbooks/first-owner-provisioning.md',
  'docs/runbooks/owner-totp-recovery.md',
  'docs/runbooks/integration-token-rotation.md',
  'docs/reference/cloud-configuration.md',
  'docs/reference/cloud-authentication-api.md',
  'docs/engineering/cloud-identity-validation.md',
];

const requiredAgentCommands = [
  'dotnet tool restore',
  'dotnet test tests/Puntiro.UnitTests/Puntiro.UnitTests.csproj --configuration Release',
  'dotnet test tests/Puntiro.IntegrationTests/Puntiro.IntegrationTests.csproj --configuration Release',
  'node scripts/check-cloud-security.mjs',
];

async function exists(target) {
  try {
    await access(target);
    return true;
  } catch {
    return false;
  }
}

async function filesBelow(directory) {
  if (!await exists(directory)) return [];
  const results = [];
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    const target = path.join(directory, entry.name);
    if (entry.isDirectory()) {
      results.push(...await filesBelow(target));
    } else if (entry.isFile()) {
      results.push(target);
    }
  }
  return results.sort();
}

function enablesBodyLogging(content) {
  return /(?:RequestBody|ResponseBody)/i.test(content) &&
    /(?:HttpLogging|LoggingFields)/i.test(content);
}

function hasPopulatedAssignment(content, name) {
  const escaped = name.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  return new RegExp(`^[ \\t]*${escaped}[ \\t]*=[ \\t]*[^\\s#][^\\r\\n]*$`, 'm').test(content);
}

function configHasPopulatedJsonValue(content, pathPattern) {
  try {
    const value = JSON.parse(content);
    const visit = (node, segments = []) => {
      if (node === null || typeof node !== 'object') {
        return typeof node === 'string' && node.trim().length > 0 && pathPattern.test(segments.join('__'));
      }
      return Object.entries(node).some(([key, child]) => visit(child, [...segments, key]));
    };
    return visit(value);
  } catch {
    return false;
  }
}

async function validateRuntime(root, errors) {
  const cloudRoot = path.join(root, 'apps', 'cloud');
  for (const target of await filesBelow(cloudRoot)) {
    if (path.extname(target) !== '.cs') continue;
    const content = await readFile(target, 'utf8');
    if (/\.Database\.(?:Migrate|MigrateAsync|EnsureCreated|EnsureCreatedAsync)\s*\(/.test(content)) {
      const relativePath = path.relative(root, target).split(path.sep).join('/');
      errors.push(`${relativePath} must not migrate the production database at runtime`);
    }
  }

  const appsettings = (await filesBelow(cloudRoot)).filter(target =>
    /^appsettings(?:\.[^.]+)?\.json$/i.test(path.basename(target)));
  for (const target of appsettings) {
    const content = await readFile(target, 'utf8');
    if (enablesBodyLogging(content)) {
      const relativePath = path.relative(root, target).split(path.sep).join('/');
      errors.push(`${relativePath} must not enable request body logging`);
    }
  }

  const programPath = path.join(cloudRoot, 'Program.cs');
  if (await exists(programPath)) {
    const program = await readFile(programPath, 'utf8');
    const forwarded = program.indexOf('UsePuntiroForwardedHeaders');
    const authentication = program.indexOf('UseAuthentication');
    if (authentication !== -1 && (forwarded === -1 || forwarded > authentication)) {
      errors.push('apps/cloud/Program.cs must apply trusted forwarded headers before authentication');
    }
  }
}

async function validateTrackedConfiguration(root, errors) {
  const candidates = [
    ...(await filesBelow(path.join(root, 'infra'))),
    ...(await filesBelow(path.join(root, 'apps', 'cloud'))).filter(target =>
      /^appsettings(?:\.[^.]+)?\.json$/i.test(path.basename(target))),
  ].filter(target => {
    const relativePath = path.relative(root, target).split(path.sep).join('/');
    return relativePath !== 'infra/compose/.env.cloud' &&
      !relativePath.startsWith('infra/compose/cloud-secrets/') &&
      !relativePath.startsWith('infra/compose/cloud-data-protection-keys/');
  });
  for (const target of [...new Set(candidates)].sort()) {
    const relativePath = path.relative(root, target).split(path.sep).join('/');
    const content = await readFile(target, 'utf8');
    const isJson = path.extname(target).toLowerCase() === '.json';
    const hasConnection = hasPopulatedAssignment(content, 'ConnectionStrings__Puntiro') ||
      (isJson && configHasPopulatedJsonValue(content, /(?:^|__)ConnectionStrings__(?:Puntiro)$/i));
    if (hasConnection) {
      errors.push(`${relativePath} must not contain a populated ConnectionStrings__Puntiro value`);
    }

    const hasHmac = /Puntiro__Security__(?:Session|Recovery|Integration)Hmac__Keys__[A-Za-z0-9_-]+[ \t]*=[ \t]*[^\s#][^\r\n]*$/m.test(content) ||
      (isJson && configHasPopulatedJsonValue(
        content,
        /Puntiro__Security__(?:Session|Recovery|Integration)Hmac__Keys__[A-Za-z0-9_-]+$/i,
      ));
    if (hasHmac) {
      errors.push(`${relativePath} must not contain populated HMAC key material`);
    }

    const hasOtherSecret = [
      'POSTGRES_PASSWORD',
      'PUNTIRO_TEST_POSTGRES',
      'Puntiro__Security__DataProtectionCertificatePassword',
    ].some(name => hasPopulatedAssignment(content, name));
    if (hasOtherSecret) {
      errors.push(`${relativePath} must not contain populated secret configuration`);
    }

    if (/pnt_live_[A-Za-z0-9_-]{22}\.[A-Za-z0-9_-]{43}/.test(content)) {
      errors.push(`${relativePath} must not contain an integration token`);
    }
  }
}

async function validateCookiePolicy(root, errors) {
  const authPath = path.join(root, 'apps/cloud/Auth/AdminSessionAuthenticationHandler.cs');
  const endpointPath = path.join(root, 'apps/cloud/Endpoints/AdminAuthEndpoints.cs');
  if (!await exists(authPath) || !await exists(endpointPath)) return;
  const auth = await readFile(authPath, 'utf8');
  const endpoints = await readFile(endpointPath, 'utf8');
  const valid = /__Host-puntiro_session/.test(auth) &&
    /Secure\s*=\s*true/.test(endpoints) &&
    /HttpOnly\s*=\s*true/.test(endpoints) &&
    /SameSite\s*=\s*SameSiteMode\.Strict/.test(endpoints) &&
    /Path\s*=\s*"\/"/.test(endpoints) &&
    !/Domain\s*=/.test(endpoints);
  if (!valid) {
    errors.push('Admin session cookie policy must be Secure, HttpOnly, SameSite=Strict, Path=/ and host-only');
  }
}

async function validateCompose(root, errors) {
  const target = path.join(root, 'infra/compose/cloud-development.yml');
  if (!await exists(target)) return;
  const content = await readFile(target, 'utf8');
  if (!/^\s*image:\s*postgres:17\.10-bookworm\s*$/m.test(content)) {
    errors.push('infra/compose/cloud-development.yml must pin postgres:17.10-bookworm');
  }
  if (!/["']127\.0\.0\.1:\$\{PUNTIRO_POSTGRES_PORT(?::-[^}]*)?\}:5432["']/.test(content)) {
    errors.push('infra/compose/cloud-development.yml must publish PostgreSQL on loopback only');
  }
  if (!/pg_isready/.test(content)) {
    errors.push('infra/compose/cloud-development.yml must define pg_isready healthcheck');
  }
  if (!/puntiro-cloud-postgres:\/var\/lib\/postgresql\/data/.test(content) ||
      !/^volumes:\s*$[\s\S]*^\s{2}puntiro-cloud-postgres:\s*$/m.test(content)) {
    errors.push('infra/compose/cloud-development.yml must use a named PostgreSQL data volume');
  }
}

async function validateProxyBoundary(root, errors) {
  const target = path.join(root, 'apps/cloud/Configuration/CloudServiceCollectionExtensions.cs');
  if (!await exists(target)) return;
  const content = await readFile(target, 'utf8');
  const valid = /ForwardedHeaders\.XForwardedFor/.test(content) &&
    /ForwardedHeaders\.XForwardedProto/.test(content) &&
    /KnownProxies\.Add\s*\(/.test(content) &&
    /KnownIPNetworks\.Add\s*\(/.test(content) &&
    /ForwardLimit\s*=\s*1/.test(content) &&
    /RequireHeaderSymmetry\s*=\s*true/.test(content) &&
    !/KnownProxies\.Clear\s*\(\s*\)[\s\S]{0,160}KnownIPNetworks\.Clear\s*\(\s*\)[\s\S]{0,160}ForwardLimit\s*=\s*null/.test(content) &&
    !/KnownIPNetworks\.Clear\s*\(\s*\)[\s\S]{0,160}KnownProxies\.Clear\s*\(\s*\)[\s\S]{0,160}ForwardLimit\s*=\s*null/.test(content);
  if (!valid) {
    errors.push('Cloud forwarded headers must use an explicit trusted proxy/network allowlist and ForwardLimit=1');
  }
  if (!/ASPNETCORE_FORWARDEDHEADERS_ENABLED/.test(content)) {
    errors.push('Cloud must reject ASPNETCORE_FORWARDEDHEADERS_ENABLED because it clears the trust boundary');
  }
}

async function validateAgents(root, errors) {
  const target = path.join(root, 'AGENTS.md');
  if (!await exists(target)) return;
  const content = await readFile(target, 'utf8');
  for (const command of requiredAgentCommands) {
    if (!content.includes(command)) errors.push(`AGENTS.md must include: ${command}`);
  }
}

export async function validateCloudSecurity(rootUrl) {
  const root = fileURLToPath(rootUrl);
  const errors = [];

  await validateRuntime(root, errors);
  await validateTrackedConfiguration(root, errors);
  await validateCookiePolicy(root, errors);
  await validateCompose(root, errors);
  await validateProxyBoundary(root, errors);
  for (const relativePath of requiredArtifacts) {
    if (!await exists(path.join(root, relativePath))) {
      errors.push(`Missing cloud security artifact: ${relativePath}`);
    }
  }
  await validateAgents(root, errors);
  return errors;
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? '').href) {
  const errors = await validateCloudSecurity(new URL('../', import.meta.url));
  if (errors.length > 0) {
    for (const error of errors) console.error(error);
    process.exitCode = 1;
  }
}
