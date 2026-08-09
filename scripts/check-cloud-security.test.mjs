import assert from 'node:assert/strict';
import { mkdir, mkdtemp, rm, writeFile } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { pathToFileURL } from 'node:url';
import { validateCloudSecurity } from './check-cloud-security.mjs';

const validFiles = {
  'AGENTS.md': `# Puntiro Repository Instructions
## Cloud Identity Validation
dotnet tool restore
dotnet test tests/Puntiro.UnitTests/Puntiro.UnitTests.csproj --configuration Release
dotnet test tests/Puntiro.IntegrationTests/Puntiro.IntegrationTests.csproj --configuration Release
node scripts/check-cloud-security.mjs
`,
  'apps/cloud/Program.cs': 'app.UsePuntiroForwardedHeaders();\napp.UseAuthentication();\napp.Run();\n',
  'apps/cloud/appsettings.json': JSON.stringify({
    Puntiro: { Security: { LoginIpLimit: 10 } },
  }),
  'apps/cloud/Auth/AdminSessionAuthenticationHandler.cs':
    'public const string CookieName = "__Host-puntiro_session";\n',
  'apps/cloud/Endpoints/AdminAuthEndpoints.cs': `new CookieOptions {
    Secure = true,
    HttpOnly = true,
    SameSite = SameSiteMode.Strict,
    Path = "/"
  };\n`,
  'apps/cloud/Configuration/CloudServiceCollectionExtensions.cs': `
    configuration["ASPNETCORE_FORWARDEDHEADERS_ENABLED"] is forbidden;
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Add(proxy);
    options.KnownIPNetworks.Add(network);
    options.ForwardLimit = 1;
    options.RequireHeaderSymmetry = true;
  `,
  'infra/compose/cloud-development.yml': `services:
  postgres:
    image: postgres:17.10-bookworm
    ports:
      - "127.0.0.1:\${PUNTIRO_POSTGRES_PORT}:5432"
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U \$\${POSTGRES_USER} -d \$\${POSTGRES_DB}"]
    volumes:
      - puntiro-cloud-postgres:/var/lib/postgresql/data
volumes:
  puntiro-cloud-postgres:
`,
  'infra/compose/.env.cloud.example': `PUNTIRO_POSTGRES_PORT=
POSTGRES_DB=
POSTGRES_USER=
POSTGRES_PASSWORD=
PUNTIRO_TEST_POSTGRES=
ConnectionStrings__Puntiro=
Puntiro__Security__SessionHmac__Keys__v1=
`,
  'infra/compose/.env.cloud': `ConnectionStrings__Puntiro=local-ignored-value
Puntiro__Security__SessionHmac__Keys__v1=local-ignored-value
`,
  'docs/adr/0002-global-identity-and-credentials.md': '# Global identity and credentials\n',
  'docs/modules/identity.md': '# Identity\n',
  'docs/modules/tenancy.md': '# Tenancy\n',
  'docs/modules/integrations.md': '# Integrations\n',
  'docs/runbooks/cloud-development.md': '# Cloud development\n',
  'docs/runbooks/first-owner-provisioning.md': '# First owner\n',
  'docs/runbooks/owner-totp-recovery.md': '# TOTP recovery\n',
  'docs/runbooks/integration-token-rotation.md': '# Token rotation\n',
  'docs/reference/cloud-configuration.md': `# Configuration
\`ConnectionStrings__Puntiro\` is supplied by a secret source.
`,
  'docs/reference/cloud-authentication-api.md': '# Authentication API\n',
  'docs/engineering/cloud-identity-validation.md': '# Validation\n',
};

async function createCloudFixture(t, overrides = {}) {
  const root = await mkdtemp(path.join(os.tmpdir(), 'puntiro-cloud-security-'));
  t.after(() => rm(root, { force: true, recursive: true }));
  const files = { ...validFiles, ...overrides };
  await Promise.all(Object.entries(files).map(async ([relativePath, content]) => {
    if (content === null) return;
    const target = path.join(root, relativePath);
    await mkdir(path.dirname(target), { recursive: true });
    await writeFile(target, content);
  }));
  return pathToFileURL(`${root}${path.sep}`);
}

test('repository satisfies the cloud security contract', async () => {
  assert.deepEqual(await validateCloudSecurity(new URL('../', import.meta.url)), []);
});

test('rejects runtime database migration and logged auth bodies', async t => {
  const fixture = await createCloudFixture(t, {
    'apps/cloud/Program.cs': 'await db.Database.MigrateAsync();',
    'apps/cloud/appsettings.json': '{"HttpLogging":{"LoggingFields":"RequestBody"}}',
  });
  assert.deepEqual(await validateCloudSecurity(fixture), [
    'apps/cloud/Program.cs must not migrate the production database at runtime',
    'apps/cloud/appsettings.json must not enable request body logging',
  ]);
});

test('rejects populated credential configuration but allows reference names and blanks', async t => {
  const fixture = await createCloudFixture(t, {
    'infra/compose/.env.cloud.example': `ConnectionStrings__Puntiro=Host=database.example
Puntiro__Security__SessionHmac__Keys__v1=ZmFrZS1idXQtcG9wdWxhdGVk
Puntiro__Security__RecoveryHmac__Keys__v1=
`,
  });
  assert.deepEqual(await validateCloudSecurity(fixture), [
    'infra/compose/.env.cloud.example must not contain a populated ConnectionStrings__Puntiro value',
    'infra/compose/.env.cloud.example must not contain populated HMAC key material',
  ]);
});

test('rejects other populated runtime secrets and canonical raw tokens', async t => {
  const fixture = await createCloudFixture(t, {
    'infra/compose/.env.cloud.example': `PUNTIRO_TEST_POSTGRES=Host=database.example
Puntiro__Security__DataProtectionCertificatePassword=populated
`,
    'apps/cloud/appsettings.Production.json': JSON.stringify({
      token: `pnt_live_${'A'.repeat(22)}.${'B'.repeat(43)}`,
    }),
  });
  assert.deepEqual(await validateCloudSecurity(fixture), [
    'apps/cloud/appsettings.Production.json must not contain an integration token',
    'infra/compose/.env.cloud.example must not contain populated secret configuration',
  ]);
});

test('rejects missing cookie security attributes', async t => {
  const fixture = await createCloudFixture(t, {
    'apps/cloud/Endpoints/AdminAuthEndpoints.cs': 'new CookieOptions { Secure = true };',
  });
  assert.deepEqual(await validateCloudSecurity(fixture), [
    'Admin session cookie policy must be Secure, HttpOnly, SameSite=Strict, Path=/ and host-only',
  ]);
});

test('rejects floating PostgreSQL tags and unsafe listener exposure', async t => {
  const fixture = await createCloudFixture(t, {
    'infra/compose/cloud-development.yml': `services:
  postgres:
    image: postgres:17
    ports:
      - "5432:5432"
`,
  });
  assert.deepEqual(await validateCloudSecurity(fixture), [
    'infra/compose/cloud-development.yml must pin postgres:17.10-bookworm',
    'infra/compose/cloud-development.yml must publish PostgreSQL on loopback only',
    'infra/compose/cloud-development.yml must define pg_isready healthcheck',
    'infra/compose/cloud-development.yml must use a named PostgreSQL data volume',
  ]);
});

test('rejects an enabled forwarded-header boundary without an explicit trust allowlist', async t => {
  const fixture = await createCloudFixture(t, {
    'apps/cloud/Configuration/CloudServiceCollectionExtensions.cs': `
      configuration["ASPNETCORE_FORWARDEDHEADERS_ENABLED"] is forbidden;
      ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
      options.KnownIPNetworks.Clear();
      options.KnownProxies.Clear();
      options.ForwardLimit = null;
    `,
  });
  assert.deepEqual(await validateCloudSecurity(fixture), [
    'Cloud forwarded headers must use an explicit trusted proxy/network allowlist and ForwardLimit=1',
  ]);
});

test('rejects the platform shortcut that trusts forwarding headers without the allowlist', async t => {
  const fixture = await createCloudFixture(t, {
    'apps/cloud/Configuration/CloudServiceCollectionExtensions.cs': validFiles[
      'apps/cloud/Configuration/CloudServiceCollectionExtensions.cs'
    ].replace('configuration["ASPNETCORE_FORWARDEDHEADERS_ENABLED"] is forbidden;\n', ''),
  });
  assert.deepEqual(await validateCloudSecurity(fixture), [
    'Cloud must reject ASPNETCORE_FORWARDEDHEADERS_ENABLED because it clears the trust boundary',
  ]);
});

test('requires forwarded headers before authentication and rate limiting', async t => {
  const fixture = await createCloudFixture(t, {
    'apps/cloud/Program.cs': 'app.UseAuthentication();\napp.UsePuntiroForwardedHeaders();\napp.Run();\n',
  });
  assert.deepEqual(await validateCloudSecurity(fixture), [
    'apps/cloud/Program.cs must apply trusted forwarded headers before authentication',
  ]);
});

test('requires cloud module and operational documentation', async t => {
  const fixture = await createCloudFixture(t, {
    'docs/modules/integrations.md': null,
    'docs/runbooks/cloud-development.md': null,
    'docs/reference/cloud-configuration.md': null,
  });
  assert.deepEqual(await validateCloudSecurity(fixture), [
    'Missing cloud security artifact: docs/modules/integrations.md',
    'Missing cloud security artifact: docs/runbooks/cloud-development.md',
    'Missing cloud security artifact: docs/reference/cloud-configuration.md',
  ]);
});

test('requires all cloud validation commands in AGENTS.md', async t => {
  const fixture = await createCloudFixture(t, {
    'AGENTS.md': '# Puntiro Repository Instructions\n',
  });
  assert.deepEqual(await validateCloudSecurity(fixture), [
    'AGENTS.md must include: dotnet tool restore',
    'AGENTS.md must include: dotnet test tests/Puntiro.UnitTests/Puntiro.UnitTests.csproj --configuration Release',
    'AGENTS.md must include: dotnet test tests/Puntiro.IntegrationTests/Puntiro.IntegrationTests.csproj --configuration Release',
    'AGENTS.md must include: node scripts/check-cloud-security.mjs',
  ]);
});
