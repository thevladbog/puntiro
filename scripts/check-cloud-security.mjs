import { execFile as execFileCallback } from 'node:child_process';
import { access, readFile } from 'node:fs/promises';
import { fileURLToPath, pathToFileURL } from 'node:url';
import path from 'node:path';
import { promisify } from 'node:util';

const execFile = promisify(execFileCallback);

const requiredArtifacts = [
  'apps/cloud/Program.cs',
  'apps/cloud/appsettings.json',
  'apps/cloud/Auth/AdminSessionAuthenticationHandler.cs',
  'apps/cloud/Endpoints/AdminAuthEndpoints.cs',
  'apps/cloud/Configuration/CloudServiceCollectionExtensions.cs',
  'infra/compose/cloud-development.yml',
  'infra/compose/.env.cloud.example',
  'infra/compose/cloud-runtime.env.example',
  'scripts/run-with-cloud-env.mjs',
  'scripts/preflight-cloud-runtime.mjs',
  'scripts/cloud-runtime-material.mjs',
  'scripts/validate-cloud-runtime-env.mjs',
  'docs/adr/0003-global-identity-and-credentials.md',
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
  'corepack pnpm test:cloud:contracts',
  'corepack pnpm test:cloud:compose',
  'node scripts/preflight-cloud-runtime.mjs --compose-env infra/compose/.env.cloud --runtime-env infra/compose/cloud-runtime.env',
];

async function exists(target) {
  try {
    await access(target);
    return true;
  } catch {
    return false;
  }
}

function enablesBodyLogging(content) {
  return /HttpLogging/i.test(content) &&
    /(?:RequestBody|ResponseBody|BodyLogLimit|LoggingFields["']?\s*:\s*["']?All\b)/i.test(content);
}

const integrationTokenPattern = /pnt_(?:live|test)_[A-Za-z0-9_-]{16,32}\.[A-Za-z0-9+/_-]{32,64}={0,2}/;

function containsIntegrationToken(content) {
  if (integrationTokenPattern.test(content)) return true;
  const encodedCandidates = content.match(/(?<![A-Za-z0-9+/_-])[A-Za-z0-9+/_-]{64,180}={0,2}(?![A-Za-z0-9+/_-])/g) ?? [];
  for (const candidate of encodedCandidates) {
    const normalized = candidate.replaceAll('-', '+').replaceAll('_', '/');
    try {
      const decoded = Buffer.from(normalized, 'base64');
      if (decoded.length >= 48 && decoded.length <= 128 &&
          decoded.toString('base64').replace(/=+$/, '') === normalized.replace(/=+$/, '') &&
          integrationTokenPattern.test(decoded.toString('utf8'))) return true;
    } catch {
      // Ignore bounded non-base64 configuration values.
    }
  }
  return false;
}

async function repositoryFiles(root, errors) {
  try {
    const { stdout } = await execFile(
      'git',
      ['-C', root, 'ls-files', '-z', '--cached', '--others', '--exclude-standard'],
      { encoding: 'utf8', maxBuffer: 16 * 1024 * 1024 },
    );
    return stdout.split('\0').filter(Boolean).sort();
  } catch {
    errors.push('Unable to enumerate tracked runtime configuration safely');
    return [];
  }
}

function unquote(raw) {
  let value = raw.trim();
  const comment = value.search(/\s+#/);
  if (comment !== -1) value = value.slice(0, comment).trimEnd();
  if (value.length >= 2 &&
      ((value.startsWith('"') && value.endsWith('"')) ||
       (value.startsWith("'") && value.endsWith("'")))) {
    return value.slice(1, -1);
  }
  return value;
}

function jsonEntries(content) {
  try {
    const root = JSON.parse(content);
    const entries = [];
    const visit = (node, segments) => {
      if (node === null || typeof node !== 'object') {
        if (typeof node === 'string') entries.push([segments.join('__'), node]);
        return;
      }
      for (const [key, child] of Object.entries(node)) visit(child, [...segments, key]);
    };
    visit(root, []);
    return entries;
  } catch {
    return [];
  }
}

function lineConfigEntries(content) {
  const entries = [];
  const yamlStack = [];
  for (const line of content.split(/\r?\n/)) {
    if (line.trim().length === 0 || line.trimStart().startsWith('#')) continue;
    const assignment = line.match(
      /^\s*(?:-\s*)?([A-Za-z_][A-Za-z0-9_.:-]*)\s*=\s*(.*?)\s*$/,
    );
    if (assignment) {
      entries.push([assignment[1], unquote(assignment[2])]);
      continue;
    }
    const yaml = line.match(/^(\s*)(?:-\s*)?["']?([A-Za-z_][A-Za-z0-9_.-]*)["']?\s*:\s*(.*)$/);
    if (!yaml) continue;
    const indent = yaml[1].replaceAll('\t', '  ').length;
    while (yamlStack.length > 0 && yamlStack.at(-1).indent >= indent) yamlStack.pop();
    const pathSegments = [...yamlStack.map(item => item.key), yaml[2]];
    if (yaml[3].trim().length === 0) {
      yamlStack.push({ indent, key: yaml[2] });
    } else {
      entries.push([pathSegments.join('__'), unquote(yaml[3])]);
    }
  }
  return entries;
}

function inlineYamlEntries(content) {
  const entries = [];
  for (const inline of content.matchAll(/\{([^{}\r\n]+)\}/g)) {
    if (inline.index > 0 && content[inline.index - 1] === '$') continue;
    for (const item of inline[1].split(',')) {
      const match = item.match(
        /^\s*["']?([A-Za-z_][A-Za-z0-9_.-]*)["']?\s*:\s*(.*?)\s*$/,
      );
      if (match) entries.push([match[1], unquote(match[2])]);
    }
  }
  return entries;
}

function xmlEntries(content) {
  const entries = [];
  for (const element of content.matchAll(/<(?:add|entry|property)\b([^>]*)>/gi)) {
    const attributes = new Map();
    for (const attribute of element[1].matchAll(
      /([A-Za-z_][A-Za-z0-9_.:-]*)\s*=\s*["']([^"']*)["']/g,
    )) {
      attributes.set(attribute[1].toLowerCase(), attribute[2]);
    }
    const name = attributes.get('key') ?? attributes.get('name');
    if (name && attributes.has('value')) entries.push([name, attributes.get('value')]);
  }
  for (const match of content.matchAll(
    /<([A-Za-z_][A-Za-z0-9_.:-]*)\b[^>]*>\s*([^<]*?)\s*<\/\1\s*>/g,
  )) {
    entries.push([match[1], unquote(match[2])]);
  }
  const stack = [];
  for (const token of content.matchAll(/<[^>]+>|[^<]+/g)) {
    if (!token[0].startsWith('<')) {
      const value = unquote(token[0]).trim();
      if (value.length > 0 && stack.length > 0) entries.push([stack.join('__'), value]);
      continue;
    }
    if (/^<\s*(?:\?|!)/.test(token[0])) continue;
    const closing = token[0].match(/^<\s*\/\s*([A-Za-z_][A-Za-z0-9_.:-]*)/);
    if (closing) {
      if (stack.at(-1) === closing[1]) stack.pop();
      continue;
    }
    const opening = token[0].match(/^<\s*([A-Za-z_][A-Za-z0-9_.:-]*)/);
    if (opening && !/\/\s*>$/.test(token[0])) stack.push(opening[1]);
  }
  return entries;
}

function configEntries(content) {
  return [
    ...jsonEntries(content),
    ...lineConfigEntries(content),
    ...inlineYamlEntries(content),
    ...xmlEntries(content),
  ];
}

function isConfigurationPath(relativePath) {
  const basename = path.basename(relativePath).toLowerCase();
  const extension = path.extname(basename);
  const segments = relativePath.toLowerCase().split('/');
  if (segments.some(segment =>
    ['.github', 'config', 'configs', 'configuration', 'deploy', 'deployment', 'infra'].includes(segment)) ||
    ['.json', '.yml', '.yaml', '.env', '.config', '.conf', '.toml', '.ini', '.properties', '.xml']
      .includes(extension) ||
    basename.startsWith('.env')) return true;
  if (['.cs', '.js', '.jsx', '.mjs', '.cjs', '.ts', '.tsx', '.md', '.txt'].includes(extension)) {
    return false;
  }
  return /(?:^|[._-])(?:appsettings|compose|dockerfile|env|runtime|settings|secrets?|credentials?)(?:[._-]|$)/
    .test(basename);
}

function isReferenceValue(value) {
  const trimmed = unquote(value).trim();
  if (trimmed.length === 0) return true;
  if (/^\$\{\{[^}]+\}\}$/.test(trimmed)) return true;
  const compose = trimmed.match(/^\$\{[A-Za-z_][A-Za-z0-9_]*(?:(:\?|\?)(?:[^}]*))?\}$/);
  if (compose) return true;
  const fallback = trimmed.match(
    /^\$\{[A-Za-z_][A-Za-z0-9_]*(?::?-)(.*)\}$/,
  );
  if (fallback) {
    const fallbackValue = fallback[1].trim();
    return fallbackValue.length === 0 || isReferenceValue(fallbackValue);
  }
  return /^\$[A-Za-z_][A-Za-z0-9_]*$/.test(trimmed) ||
    /^%[A-Za-z_][A-Za-z0-9_]*%$/.test(trimmed) ||
    /^<[^>]+>$/.test(trimmed);
}

function normalizedName(name) {
  return name.replace(/[^A-Za-z0-9]/g, '').toLowerCase();
}

function isConnectionName(name) {
  const normalized = normalizedName(name);
  return normalized.endsWith('connectionstringspuntiro') ||
    normalized.endsWith('puntirotestpostgres');
}

function isHmacName(name) {
  return /puntirosecurity(?:session|recovery|integration)hmackeys[a-z0-9]+$/
    .test(normalizedName(name));
}

function isSecretName(name) {
  const normalized = name.replace(/([a-z0-9])([A-Z])/g, '$1_$2').toUpperCase();
  const segments = normalized.split(/[^A-Z0-9]+/).filter(Boolean);
  const joined = segments.join('_');
  return /(?:^|_)(?:PASSWORD|SECRET|TOKEN|API_KEY|AUTH_TOKEN|ACCESS_TOKEN|BEARER_TOKEN|CLIENT_SECRET|SECRET_KEY|PRIVATE_KEY|ACCESS_KEY|INTEGRATION_TOKEN)$/.test(joined);
}

function isExplicitCiFixture(relativePath, name, value) {
  if (!relativePath.startsWith('.github/')) return false;
  if (/(?:^|__)POSTGRES_PASSWORD$/i.test(name)) {
    return value === 'puntiro-ci-fixture-only';
  }
  if (/(?:^|__)PUNTIRO_TEST_POSTGRES$/i.test(name)) {
    return value === 'Host=127.0.0.1;Port=5432;Database=puntiro_ci;Username=puntiro_ci;Password=puntiro-ci-fixture-only;Include Error Detail=false';
  }
  return false;
}

async function validateRuntime(root, errors, tracked) {
  const productionCode = tracked.filter(relativePath =>
    /^(?:apps|src|tools)\//.test(relativePath) && relativePath.endsWith('.cs'));
  for (const relativePath of productionCode) {
    const target = path.join(root, relativePath);
    const content = await readFile(target, 'utf8');
    if (/\bIMigrator\b|\b(?:Migrate|MigrateAsync|EnsureCreated|EnsureCreatedAsync)\s*\(/.test(content)) {
      errors.push(`${relativePath} must not migrate the production database at runtime`);
    }
    if (/\b(?:AddHttpLogging|UseHttpLogging)\s*\(|\bHttpLoggingFields\s*\.\s*(?:[A-Za-z]*Body|All)\b|\b(?:RequestBodyLogLimit|ResponseBodyLogLimit)\b/.test(content)) {
      errors.push(`${relativePath} must not configure request or response body logging`);
    }
  }

  const runtimeConfigs = tracked.filter(relativePath =>
    /^(?:apps|src|tools)\//.test(relativePath) &&
    /^appsettings(?:\.[^.]+)?\.json$/i.test(path.basename(relativePath)));
  for (const relativePath of runtimeConfigs) {
    const target = path.join(root, relativePath);
    const content = await readFile(target, 'utf8');
    if (enablesBodyLogging(content)) {
      errors.push(`${relativePath} must not enable request body logging`);
    }
  }

  const programPath = path.join(root, 'apps', 'cloud', 'Program.cs');
  if (await exists(programPath)) {
    const program = await readFile(programPath, 'utf8');
    const forwarded = program.indexOf('UsePuntiroForwardedHeaders');
    const authentication = program.indexOf('UseAuthentication');
    if (authentication !== -1 && (forwarded === -1 || forwarded > authentication)) {
      errors.push('apps/cloud/Program.cs must apply trusted forwarded headers before authentication');
    }
  }
}

async function validateTrackedConfiguration(root, errors, tracked) {
  for (const relativePath of tracked) {
    const target = path.join(root, relativePath);
    const bytes = await readFile(target);
    if (bytes.includes(0)) continue;
    let content;
    try {
      content = new TextDecoder('utf-8', { fatal: true }).decode(bytes);
    } catch {
      continue;
    }

    if (containsIntegrationToken(content)) {
      errors.push(`${relativePath} must not contain an integration token`);
    }
    if (!isConfigurationPath(relativePath)) continue;

    const entries = configEntries(content);
    const populated = ([name, value]) =>
      !isReferenceValue(value) && !isExplicitCiFixture(relativePath, name, value);
    const hasConnection = entries.some(entry =>
      isConnectionName(entry[0]) && populated(entry));
    if (hasConnection) {
      errors.push(`${relativePath} must not contain a populated ConnectionStrings__Puntiro value`);
    }

    const hasHmac = entries.some(entry =>
      isHmacName(entry[0]) && populated(entry));
    if (hasHmac) {
      errors.push(`${relativePath} must not contain populated HMAC key material`);
    }

    const hasOtherSecret = entries.some(entry => isSecretName(entry[0]) && populated(entry));
    if (hasOtherSecret) {
      errors.push(`${relativePath} must not contain populated secret configuration`);
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
  if (!/env_file:\s*[\s\S]*PUNTIRO_CLOUD_RUNTIME_ENV_FILE[\s\S]*format:\s*raw/.test(content) ||
      !/required:\s*true/.test(content) ||
      /Puntiro__Proxy__Known(?:Proxies|Networks)__\d+\s*:/.test(content) ||
      /Puntiro__Security__(?:Session|Recovery|Integration)Hmac__Keys__[A-Za-z0-9_-]+\s*:/.test(content)) {
    errors.push('infra/compose/cloud-development.yml must pass dynamic Cloud runtime values through the ignored raw env_file');
  }
  if (!/type:\s*bind[\s\S]{0,180}source:\s*\$\{Puntiro__Security__DataProtectionKeysPath[\s\S]{0,180}target:\s*\/var\/lib\/puntiro\/data-protection-keys/.test(content) ||
      /puntiro-cloud-data-protection-keys/.test(content) ||
      (content.match(/create_host_path:\s*false/g) ?? []).length < 2) {
    errors.push('infra/compose/cloud-development.yml must bind the host provisioning Data Protection ring into Cloud');
  }
  if (!/user:\s*["']?\$\{PUNTIRO_CLOUD_UID[^}]*\}:\$\{PUNTIRO_CLOUD_GID/.test(content)) {
    errors.push('infra/compose/cloud-development.yml must run Cloud as the configured service uid and gid');
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

async function validateRestoreOrdering(root, errors) {
  const target = path.join(root, 'docs', 'runbooks', 'cloud-development.md');
  if (!await exists(target)) return;
  const content = await readFile(target, 'utf8');
  const restoreStart = content.indexOf('PUNTIRO_RESTORE_PROJECT');
  if (restoreStart === -1) {
    errors.push('docs/runbooks/cloud-development.md must validate the restore environment before creating or starting restore services');
    return;
  }
  const restore = content.slice(restoreStart);
  const validation = restore.indexOf('node scripts/validate-cloud-runtime-env.mjs');
  const operation = restore.search(
    /docker compose[\s\S]{0,400}-p\s+"?\$PUNTIRO_RESTORE_PROJECT"?[\s\S]{0,240}\b(?:create|up|start)\b/,
  );
  if (validation === -1 || operation === -1 || validation > operation) {
    errors.push('docs/runbooks/cloud-development.md must validate the restore environment before creating or starting restore services');
  }
}

async function validateNormalPreflightOrdering(root, errors) {
  const target = path.join(root, 'docs', 'runbooks', 'cloud-development.md');
  if (!await exists(target)) return;
  const content = await readFile(target, 'utf8');
  const preflight = content.indexOf('node scripts/preflight-cloud-runtime.mjs');
  const cloudStart = content.search(
    /docker compose[\s\S]{0,500}--profile\s+cloud-runtime[\s\S]{0,160}\b(?:create|up|start)\b/,
  );
  if (preflight === -1 || cloudStart === -1 || preflight > cloudStart) {
    errors.push('docs/runbooks/cloud-development.md must run normal Cloud preflight before creating or starting Cloud');
  }
}

export async function validateCloudSecurity(rootUrl) {
  const root = fileURLToPath(rootUrl);
  const errors = [];
  const tracked = await repositoryFiles(root, errors);

  await validateRuntime(root, errors, tracked);
  await validateTrackedConfiguration(root, errors, tracked);
  await validateCookiePolicy(root, errors);
  await validateCompose(root, errors);
  await validateProxyBoundary(root, errors);
  for (const relativePath of requiredArtifacts) {
    if (!await exists(path.join(root, relativePath))) {
      errors.push(`Missing cloud security artifact: ${relativePath}`);
    }
  }
  await validateAgents(root, errors);
  await validateRestoreOrdering(root, errors);
  await validateNormalPreflightOrdering(root, errors);
  return errors;
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? '').href) {
  const errors = await validateCloudSecurity(new URL('../', import.meta.url));
  if (errors.length > 0) {
    for (const error of errors) console.error(error);
    process.exitCode = 1;
  }
}
