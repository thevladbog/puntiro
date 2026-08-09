import assert from 'node:assert/strict';
import { execFile as execFileCallback } from 'node:child_process';
import { mkdir, mkdtemp, rm, writeFile } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { pathToFileURL } from 'node:url';
import { promisify } from 'node:util';
import { validateCloudSecurity } from './check-cloud-security.mjs';

const execFile = promisify(execFileCallback);

const validFiles = {
  '.gitignore': `infra/compose/.env.cloud
infra/compose/cloud-runtime.env
infra/compose/cloud-secrets/
infra/compose/cloud-data-protection-keys/
`,
  'AGENTS.md': `# Puntiro Repository Instructions
## Cloud Identity Validation
dotnet tool restore
dotnet test tests/Puntiro.UnitTests/Puntiro.UnitTests.csproj --configuration Release
dotnet test tests/Puntiro.IntegrationTests/Puntiro.IntegrationTests.csproj --configuration Release
node scripts/check-cloud-security.mjs
corepack pnpm test:cloud:contracts
corepack pnpm test:cloud:compose
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
  cloud:
    env_file:
      - path: \${PUNTIRO_CLOUD_RUNTIME_ENV_FILE}
        format: raw
    volumes:
      - type: bind
        source: \${Puntiro__Security__DataProtectionKeysPath}
        target: /var/lib/puntiro/data-protection-keys
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
  'infra/compose/cloud-runtime.env.example': `ConnectionStrings__Puntiro=
Puntiro__Security__SessionHmac__Keys__v1=
`,
  'scripts/run-with-cloud-env.mjs': '// fixture\n',
  'scripts/validate-cloud-runtime-env.mjs': '// fixture\n',
  'infra/compose/.env.cloud': `ConnectionStrings__Puntiro=local-ignored-value
Puntiro__Security__SessionHmac__Keys__v1=local-ignored-value
`,
  'docs/adr/0003-global-identity-and-credentials.md': '# Global identity and credentials\n',
  'docs/modules/identity.md': '# Identity\n',
  'docs/modules/tenancy.md': '# Tenancy\n',
  'docs/modules/integrations.md': '# Integrations\n',
  'docs/runbooks/cloud-development.md': `# Cloud development
PUNTIRO_RESTORE_PROJECT=puntiro-restore-drill
node scripts/validate-cloud-runtime-env.mjs --restore-project "$PUNTIRO_RESTORE_PROJECT"
docker compose -p "$PUNTIRO_RESTORE_PROJECT" up -d postgres
`,
  'docs/runbooks/first-owner-provisioning.md': '# First owner\n',
  'docs/runbooks/owner-totp-recovery.md': '# TOTP recovery\n',
  'docs/runbooks/integration-token-rotation.md': '# Token rotation\n',
  'docs/reference/cloud-configuration.md': `# Configuration
\`ConnectionStrings__Puntiro\` is supplied by a secret source.
`,
  'docs/reference/cloud-authentication-api.md': '# Authentication API\n',
  'docs/engineering/cloud-identity-validation.md': '# Validation\n',
};

async function createCloudFixture(
  t,
  overrides = {},
  { forceTracked = [], untrackedFiles = {} } = {},
) {
  const root = await mkdtemp(path.join(os.tmpdir(), 'puntiro-cloud-security-'));
  t.after(() => rm(root, { force: true, recursive: true }));
  const files = { ...validFiles, ...overrides };
  await Promise.all(Object.entries(files).map(async ([relativePath, content]) => {
    if (content === null) return;
    const target = path.join(root, relativePath);
    await mkdir(path.dirname(target), { recursive: true });
    await writeFile(target, content);
  }));
  await execFile('git', ['init', '--quiet'], { cwd: root });
  await execFile('git', ['add', '.'], { cwd: root });
  for (const relativePath of forceTracked) {
    await execFile('git', ['add', '--force', '--', relativePath], { cwd: root });
  }
  await Promise.all(Object.entries(untrackedFiles).map(async ([relativePath, content]) => {
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

test('rejects migration services and code-configured body logging in every production composition', async t => {
  const fixture = await createCloudFixture(t, {
    'apps/worker/Program.cs': `RelationalDatabaseFacadeExtensions.Migrate(database);
services.AddHttpLogging(options => options.LoggingFields = HttpLoggingFields.RequestBody);
app.UseHttpLogging();
`,
    'apps/worker/appsettings.Production.json': '{"HttpLogging":{"LoggingFields":"ResponseBody"}}',
    'src/Puntiro.Modules.Identity/RuntimeBootstrap.cs':
      'var migrator = services.GetRequiredService<IMigrator>(); await migrator.MigrateAsync();\n',
    'src/Puntiro.Modules.Identity/HttpLogOptions.cs':
      'options.LoggingFields = HttpLoggingFields.All;\n',
    'tools/Puntiro.Runner/Program.cs':
      'options.ResponseBodyLogLimit = 4096; await database.EnsureCreatedAsync();\n',
  });

  const errors = await validateCloudSecurity(fixture);
  assert.ok(errors.includes('apps/worker/Program.cs must not migrate the production database at runtime'));
  assert.ok(errors.includes('apps/worker/Program.cs must not configure request or response body logging'));
  assert.ok(errors.includes('apps/worker/appsettings.Production.json must not enable request body logging'));
  assert.ok(errors.includes('src/Puntiro.Modules.Identity/RuntimeBootstrap.cs must not migrate the production database at runtime'));
  assert.ok(errors.includes('src/Puntiro.Modules.Identity/HttpLogOptions.cs must not configure request or response body logging'));
  assert.ok(errors.includes('tools/Puntiro.Runner/Program.cs must not migrate the production database at runtime'));
  assert.ok(errors.includes('tools/Puntiro.Runner/Program.cs must not configure request or response body logging'));
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
    'infra/compose/.env.cloud.example must not contain a populated ConnectionStrings__Puntiro value',
    'infra/compose/.env.cloud.example must not contain populated secret configuration',
  ]);
});

test('scans tracked yaml and deploy dotenv secret forms without returning their values', async t => {
  const encodedToken = `pnt_live_${'A'.repeat(22)}.${'B'.repeat(43)}=`;
  const base64Token = Buffer.from(`pnt_test_${'C'.repeat(22)}.${'D'.repeat(43)}`)
    .toString('base64');
  const fixture = await createCloudFixture(t, {
    '.github/workflows/runtime.yml': `env:
  Puntiro__Security__DataProtectionCertificatePassword: bounded-review-probe
`,
    'deploy/cloud.env': `ConnectionStrings__Puntiro=Host=runtime.invalid;Database=runtime
INTEGRATION_TOKEN=${encodedToken}
ENCODED_INTEGRATION_TOKEN=${base64Token}
`,
  });

  const errors = await validateCloudSecurity(fixture);
  assert.ok(errors.includes('.github/workflows/runtime.yml must not contain populated secret configuration'));
  assert.ok(errors.includes('deploy/cloud.env must not contain a populated ConnectionStrings__Puntiro value'));
  assert.ok(errors.includes('deploy/cloud.env must not contain an integration token'));
  assert.doesNotMatch(JSON.stringify(errors), /bounded-review-probe|Host=runtime\.invalid|pnt_live_A|pnt_test_C|cG50X3Rlc3Q/);
});

test('does not exempt arbitrary GitHub secrets merely because their value mentions fixture-only', async t => {
  const fixture = await createCloudFixture(t, {
    '.github/workflows/runtime.yml': `env:
  POSTGRES_PASSWORD: bounded-review-probe-fixture-only
`,
  });

  assert.deepEqual(await validateCloudSecurity(fixture), [
    '.github/workflows/runtime.yml must not contain populated secret configuration',
  ]);
});

test('rejects request body logging configured through the HttpLogging All value', async t => {
  const fixture = await createCloudFixture(t, {
    'apps/worker/appsettings.Production.json':
      '{"HttpLogging":{"LoggingFields":"All"}}',
  });

  assert.ok((await validateCloudSecurity(fixture)).includes(
    'apps/worker/appsettings.Production.json must not enable request body logging',
  ));
});

test('rejects a bounded base64 encoding of an integration token without returning it', async t => {
  const encoded = Buffer.from(`pnt_test_${'C'.repeat(22)}.${'D'.repeat(43)}`)
    .toString('base64');
  const fixture = await createCloudFixture(t, {
    'deploy/cloud.env': `ENCODED_INTEGRATION_TOKEN=${encoded}\n`,
  });

  const errors = await validateCloudSecurity(fixture);
  assert.ok(errors.includes('deploy/cloud.env must not contain an integration token'));
  assert.doesNotMatch(JSON.stringify(errors), /pnt_test_C|cG50X3Rlc3Q/);
});

test('scans a force-tracked ignored runtime env but exempts the ignored untracked local file', async t => {
  const fixture = await createCloudFixture(t, {
    'infra/compose/.env.cloud':
      'Puntiro__Security__RecoveryHmac__Keys__v-retained=bounded-review-probe\n',
  }, {
    forceTracked: ['infra/compose/.env.cloud'],
  });

  assert.deepEqual(await validateCloudSecurity(fixture), [
    'infra/compose/.env.cloud must not contain populated HMAC key material',
  ]);
});

test('does not exempt a populated local config unless it is both ignored and untracked', async t => {
  const fixture = await createCloudFixture(t, {}, {
    untrackedFiles: {
      'deploy/local-runtime.env': 'ConnectionStrings__Puntiro=bounded-review-probe\n',
    },
  });

  assert.deepEqual(await validateCloudSecurity(fixture), [
    'deploy/local-runtime.env must not contain a populated ConnectionStrings__Puntiro value',
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
    'infra/compose/cloud-development.yml must pass dynamic Cloud runtime values through the ignored raw env_file',
    'infra/compose/cloud-development.yml must bind the host provisioning Data Protection ring into Cloud',
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

test('requires restore environment validation before any restore project create or start', async t => {
  const fixture = await createCloudFixture(t, {
    'docs/runbooks/cloud-development.md': `# Cloud development
docker compose -p "$PUNTIRO_RESTORE_PROJECT" create cloud
node scripts/validate-cloud-runtime-env.mjs --restore-project "$PUNTIRO_RESTORE_PROJECT"
`,
  });

  assert.ok((await validateCloudSecurity(fixture)).includes(
    'docs/runbooks/cloud-development.md must validate the restore environment before creating or starting restore services',
  ));
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
    'AGENTS.md must include: corepack pnpm test:cloud:contracts',
    'AGENTS.md must include: corepack pnpm test:cloud:compose',
  ]);
});
