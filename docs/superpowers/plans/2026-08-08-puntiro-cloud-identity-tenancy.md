# Puntiro Cloud Identity and Tenancy Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Реализовать первый cloud-срез Puntiro: безопасный bootstrap организации и owner, глобальный identity, Argon2id/TOTP/recovery, серверные сессии, tenant membership и долгоживущие integration tokens.

**Architecture:** ASP.NET Core 10 host компонует независимые модули `Identity`, `Tenancy` и `Integrations`; каждый модуль владеет своей PostgreSQL schema, EF Core context и миграциями. Cloud выводит tenant только из проверенной server session либо integration token, а непубличный provisioning CLI использует те же application contracts, что и HTTP host.

**Tech Stack:** .NET SDK 10.0.302, ASP.NET Core 10.0.10, EF Core 10.0.10, Npgsql EF Provider 10.0.3, PostgreSQL 17.10, Argon2id 1.3.1, xUnit v3 3.2.2, Node.js 24.19.0, pnpm 11.17.0.

## Global Constraints

- Source of truth: `docs/superpowers/specs/2026-08-08-puntiro-cloud-identity-tenancy-design.md`.
- Read root `AGENTS.md` and the closest nested `AGENTS.md` before editing.
- Start every behavior change with a focused RED test, observe the intended failure, then write the minimum GREEN implementation.
- Use exact stable package versions only in `Directory.Packages.props`; project files never carry local versions.
- Before changing a dependency pin, re-check Context7, the official package source, release notes and security advisories.
- PostgreSQL tests use the real `postgres:17.10-bookworm` server; SQLite and EF in-memory are not substitutes.
- Every integration-test command assumes `PUNTIRO_TEST_POSTGRES` is already exported from an untracked local environment file; absence is a hard failure, never a skip.
- Identity, Tenancy and Integrations never read each other's tables or reference each other's projects.
- Global account email is unique after the approved normalization; organization access exists only through active membership.
- Argon2id policy is `19456 KiB`, `2` iterations, parallelism `1`, unique 16-byte salt and 32-byte hash.
- TOTP is RFC 6238 HMAC-SHA-1, six digits, 30-second period, `±1` period window, with atomic replay prevention.
- Admin session idle timeout is 30 minutes and absolute lifetime is 12 hours.
- Step-up freshness is five minutes and requires TOTP, never a recovery code.
- Admin cookie is `__Host-puntiro_session; Secure; HttpOnly; SameSite=Strict; Path=/` with no Domain.
- Integration token format is `pnt_live_<public-id>.<secret>`; raw token is shown once, default expiry is absent, maximum active count per organization is two.
- Passwords, TOTP values/secrets, recovery codes, session secrets, raw integration tokens and HMAC values never enter logs, traces, audit payloads or exceptions.
- Production Cloud never invokes `MigrateAsync()`; migrations run through an explicit deployment command.
- Keep browser/CI proof separate from Timeweb, Windows and physical hardware acceptance.
- Update useful documentation, `AGENTS.md`, dependency checks and CI in the same branch as the behavior.

Version research was refreshed on 2026-08-08. Context7 confirmed current Npgsql/EF usage (`UseNpgsql`, explicit PostgreSQL version, retry policy and separate migrations). Official sources confirm [PostgreSQL 17.10](https://www.postgresql.org/docs/release/17.10/) as the current secure patch release of the approved major, [Microsoft.AspNetCore.OpenApi 10.0.10](https://www.nuget.org/packages/Microsoft.AspNetCore.OpenApi/10.0.10), and the [xUnit v3 VSTest package set](https://xunit.net/docs/getting-started/v3/getting-started). Exact package/version queries were checked against GitHub Advisory Database with no matching advisories at planning time; execution rechecks rather than assuming this result remains current.

## Planned File Structure

```text
src/
  Puntiro.Security/                 shared crypto-safe primitives, no business entities
  Puntiro.Modules.Identity/         account, credentials, TOTP, recovery, sessions
  Puntiro.Modules.Tenancy/          organization lifecycle and memberships
  Puntiro.Modules.Integrations/     integration tokens and scopes
tools/
  Puntiro.Provisioning/             interactive bootstrap and owner TOTP recovery
tests/
  Puntiro.UnitTests/                deterministic domain and crypto tests
  Puntiro.IntegrationTests/         PostgreSQL and HTTP tests
apps/cloud/
  Auth/                             session and bearer handlers
  Endpoints/                        minimal API route groups
  Health/                           readiness checks
  Http/                             problem details, redaction, tenant context
infra/compose/
  cloud-development.yml             local PostgreSQL 17.10
docs/
  modules/                           module ownership and failure modes
  runbooks/                          bootstrap, TOTP recovery, token rotation
  adr/                               global identity and credential storage decision
```

---

### Task 1: Lock the Cloud Module and Test Boundaries

**Files:**
- Create: `.config/dotnet-tools.json`
- Create: `src/Puntiro.Security/Puntiro.Security.csproj`
- Create: `src/Puntiro.Security/SecurityModuleMarker.cs`
- Create: `src/Puntiro.Security/Properties/AssemblyInfo.cs`
- Create: `src/Puntiro.Modules.Identity/Puntiro.Modules.Identity.csproj`
- Create: `src/Puntiro.Modules.Identity/IdentityModuleMarker.cs`
- Create: `src/Puntiro.Modules.Identity/Properties/AssemblyInfo.cs`
- Create: `src/Puntiro.Modules.Tenancy/Puntiro.Modules.Tenancy.csproj`
- Create: `src/Puntiro.Modules.Tenancy/TenancyModuleMarker.cs`
- Create: `src/Puntiro.Modules.Tenancy/Properties/AssemblyInfo.cs`
- Create: `src/Puntiro.Modules.Integrations/Puntiro.Modules.Integrations.csproj`
- Create: `src/Puntiro.Modules.Integrations/IntegrationsModuleMarker.cs`
- Create: `src/Puntiro.Modules.Integrations/Properties/AssemblyInfo.cs`
- Create: `tools/Puntiro.Provisioning/Puntiro.Provisioning.csproj`
- Create: `tools/Puntiro.Provisioning/Program.cs`
- Create: `tools/Puntiro.Provisioning/Properties/AssemblyInfo.cs`
- Create: `tests/Puntiro.UnitTests/Puntiro.UnitTests.csproj`
- Create: `tests/Puntiro.IntegrationTests/Puntiro.IntegrationTests.csproj`
- Modify: `Directory.Packages.props`
- Modify: `Directory.Build.props`
- Create: `apps/cloud/packages.lock.json`
- Create: `src/Puntiro.Modules.Identity/packages.lock.json`
- Create: `src/Puntiro.Modules.Tenancy/packages.lock.json`
- Create: `src/Puntiro.Modules.Integrations/packages.lock.json`
- Create: `tests/Puntiro.UnitTests/packages.lock.json`
- Create: `tests/Puntiro.IntegrationTests/packages.lock.json`
- Modify: `Puntiro.slnx`
- Modify: `apps/cloud/Puntiro.Cloud.csproj`
- Modify: `scripts/check-foundation.mjs`
- Modify: `scripts/check-foundation.test.mjs`
- Modify: `scripts/check-dependency-policy.mjs`
- Modify: `scripts/check-dependency-policy.test.mjs`

**Interfaces:**
- Consumes: existing `Puntiro.Contracts`, central package policy and one-way application boundaries.
- Produces: buildable projects with allowed graph `Cloud -> modules -> Puntiro.Security`; `Provisioning -> Identity + Tenancy`; product apps still never reference one another.

- [ ] **Step 1: Extend the repository contract tests and record RED**

Add the new production projects to the fixture and assert these cases in `scripts/check-foundation.test.mjs`:

```js
test('cloud may compose approved modules but modules cannot reference each other', async t => {
  const { root, rootUrl } = await createFoundationFixture(t);
  await writeFile(
    path.join(root, 'src/Puntiro.Modules.Identity/Puntiro.Modules.Identity.csproj'),
    '<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><ProjectReference Include="../Puntiro.Modules.Tenancy/Puntiro.Modules.Tenancy.csproj" /></ItemGroup></Project>',
  );

  assert.deepEqual(await validateFoundation(rootUrl), [
    'src/Puntiro.Modules.Identity/Puntiro.Modules.Identity.csproj must not reference module: src/Puntiro.Modules.Tenancy/Puntiro.Modules.Tenancy.csproj',
  ]);
});
```

Add a dependency-policy test requiring exact `dotnet-ef` in the repository tool manifest:

```js
test('repository dotnet tools use exact approved versions', async () => {
  const manifest = JSON.parse(await readFile(new URL('../.config/dotnet-tools.json', import.meta.url), 'utf8'));
  assert.equal(manifest.tools['dotnet-ef'].version, '10.0.10');
  assert.deepEqual(manifest.tools['dotnet-ef'].commands, ['dotnet-ef']);
});
```

Run:

```bash
node --test scripts/check-foundation.test.mjs scripts/check-dependency-policy.test.mjs
```

Expected: FAIL because the new project graph and tool manifest do not exist.

- [ ] **Step 2: Add exact package and tool pins**

Populate `Directory.Packages.props` with these exact `PackageVersion` entries:

```xml
<ItemGroup>
  <PackageVersion Include="Konscious.Security.Cryptography.Argon2" Version="1.3.1" />
  <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.10" />
  <PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="10.0.10" />
  <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.0.10" />
  <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.10" />
  <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.8.1" />
  <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.3" />
  <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.5" />
  <PackageVersion Include="xunit.v3" Version="3.2.2" />
</ItemGroup>
```

Create `.config/dotnet-tools.json` with schema version `1`, root `true`, and exact `dotnet-ef` `10.0.10`.

Set `RestorePackagesWithLockFile=true` in `Directory.Build.props`. Generate and commit NuGet lock files for every project with external packages. CI later restores with `--locked-mode`; local dependency changes must intentionally regenerate and review these files.

- [ ] **Step 3: Create projects and enforce the reference graph**

Each module project targets inherited `net10.0`, references `Puntiro.Security`, and carries only its owned packages:

- Tenancy and Integrations reference `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Design` (`PrivateAssets=all`) and `Npgsql.EntityFrameworkCore.PostgreSQL`;
- Identity references the same EF packages, `Konscious.Security.Cryptography.Argon2`, and framework reference `Microsoft.AspNetCore.App` for Data Protection abstractions;
- Cloud references all three modules plus `Microsoft.AspNetCore.OpenApi`;
- Provisioning is an executable referencing Identity and Tenancy;
- UnitTests references Security, all modules and Provisioning;
- IntegrationTests references Cloud, all modules and Provisioning plus `Microsoft.AspNetCore.Mvc.Testing`.

Test projects set `IsTestProject=true`, `IsPackable=false`, and reference `xunit.v3`, `xunit.runner.visualstudio` with `PrivateAssets=all`, and `Microsoft.NET.Test.Sdk`. Each production module and Provisioning grants `InternalsVisibleTo` only to the two named test assemblies through its `Properties/AssemblyInfo.cs`.

Replace the fixed four-project model in `scripts/check-foundation.mjs` with named sets:

```js
const leafProjects = new Set([
  'src/Puntiro.Contracts/Puntiro.Contracts.csproj',
  'src/Puntiro.Security/Puntiro.Security.csproj',
]);
const moduleProjects = new Set([
  'src/Puntiro.Modules.Identity/Puntiro.Modules.Identity.csproj',
  'src/Puntiro.Modules.Tenancy/Puntiro.Modules.Tenancy.csproj',
  'src/Puntiro.Modules.Integrations/Puntiro.Modules.Integrations.csproj',
]);
const cloudModules = new Set(moduleProjects);
```

Allow modules to reference only Security, allow Cloud to reference Contracts plus all approved modules, allow Provisioning to reference Identity and Tenancy, and reject every module-to-module or app-to-app edge. List every new project in `Puntiro.slnx`.

- [ ] **Step 4: Run boundary, restore and build gates**

Run:

```bash
node --test scripts/check-foundation.test.mjs scripts/check-dependency-policy.test.mjs
node scripts/check-foundation.mjs
node scripts/check-dependency-policy.mjs
dotnet tool restore
dotnet restore Puntiro.slnx --use-lock-file
dotnet build Puntiro.slnx --configuration Release --no-restore
```

Expected: all commands PASS; no package version appears inside a `.csproj`.

- [ ] **Step 5: Commit the module foundation**

```bash
git add .config Directory.Build.props Directory.Packages.props Puntiro.slnx apps/cloud/Puntiro.Cloud.csproj apps/cloud/packages.lock.json src tools/Puntiro.Provisioning/Puntiro.Provisioning.csproj tools/Puntiro.Provisioning/Program.cs tests scripts/check-foundation.mjs scripts/check-foundation.test.mjs scripts/check-dependency-policy.mjs scripts/check-dependency-policy.test.mjs
git commit -m "build: add cloud module boundaries"
```

### Task 2: Implement Tenancy Lifecycle and PostgreSQL Ownership

**Files:**
- Create: `src/Puntiro.Modules.Tenancy/Contracts/ITenancyProvisioningService.cs`
- Create: `src/Puntiro.Modules.Tenancy/Contracts/ITenantAccessService.cs`
- Create: `src/Puntiro.Modules.Tenancy/Contracts/OrganizationSelectionRequiredException.cs`
- Create: `src/Puntiro.Modules.Tenancy/Domain/Organization.cs`
- Create: `src/Puntiro.Modules.Tenancy/Domain/Membership.cs`
- Create: `src/Puntiro.Modules.Tenancy/Domain/TenancySecurityEvent.cs`
- Create: `src/Puntiro.Modules.Tenancy/Domain/OrganizationSlug.cs`
- Create: `src/Puntiro.Modules.Tenancy/Persistence/TenancyDbContext.cs`
- Create: `src/Puntiro.Modules.Tenancy/Persistence/TenancyDbContextFactory.cs`
- Create: `src/Puntiro.Modules.Tenancy/Persistence/TenancyModelConfiguration.cs`
- Create: `src/Puntiro.Modules.Tenancy/Persistence/Migrations/202608080001_InitialTenancy.cs`
- Create: `src/Puntiro.Modules.Tenancy/Persistence/Migrations/202608080001_InitialTenancy.Designer.cs`
- Create: `src/Puntiro.Modules.Tenancy/Persistence/Migrations/TenancyDbContextModelSnapshot.cs`
- Create: `src/Puntiro.Modules.Tenancy/Services/TenancyProvisioningService.cs`
- Create: `src/Puntiro.Modules.Tenancy/Services/TenantAccessService.cs`
- Create: `src/Puntiro.Modules.Tenancy/TenancyModule.cs`
- Create: `tests/Puntiro.UnitTests/Tenancy/OrganizationSlugTests.cs`
- Create: `tests/Puntiro.UnitTests/Tenancy/OrganizationLifecycleTests.cs`
- Create: `tests/Puntiro.IntegrationTests/Infrastructure/PostgresDatabase.cs`
- Create: `tests/Puntiro.IntegrationTests/Infrastructure/PostgresCollection.cs`
- Create: `tests/Puntiro.IntegrationTests/Tenancy/TenancyTestScope.cs`
- Create: `tests/Puntiro.IntegrationTests/Tenancy/TenancyPersistenceTests.cs`
- Create: `docs/modules/tenancy.md`

**Interfaces:**
- Consumes: `TimeProvider`, PostgreSQL connection string and `Guid.CreateVersion7`.
- Produces:

```csharp
public interface ITenancyProvisioningService
{
    Task<OrganizationSnapshot> GetOrCreateProvisioningAsync(
        string displayName, string slug, CancellationToken cancellationToken);
    Task<MembershipSnapshot> EnsureOwnerMembershipAsync(
        Guid organizationId, Guid userId, CancellationToken cancellationToken);
    Task ActivateAsync(Guid organizationId, CancellationToken cancellationToken);
}

public interface ITenantAccessService
{
    Task<TenantAccess?> FindSingleActiveMembershipAsync(
        Guid userId, CancellationToken cancellationToken);
    Task<bool> IsActiveOwnerAsync(
        Guid organizationId, Guid userId, CancellationToken cancellationToken);
}
```

`FindSingleActiveMembershipAsync` returns null for no membership and throws a typed `OrganizationSelectionRequiredException` for more than one active membership. Stage 1 Cloud maps that future-safe condition to `409 auth.organization_selection_required`; it never chooses an arbitrary tenant.

- [ ] **Step 1: Write normalization and lifecycle RED tests**

Cover ASCII collapse and invalid slug input:

```csharp
[Theory]
[InlineData("  Moscow--Warehouse  ", "moscow-warehouse")]
[InlineData("A  B", "a-b")]
public void Normalize_returns_canonical_slug(string input, string expected) =>
    Assert.Equal(expected, OrganizationSlug.Normalize(input).Value);

[Fact]
public void Active_organization_requires_active_owner()
{
    var organization = Organization.StartProvisioning(Guid.CreateVersion7(), "Puntiro", "puntiro");
    Assert.Throws<InvalidOperationException>(() => organization.Activate(hasActiveOwner: false));
}
```

Run:

```bash
dotnet test tests/Puntiro.UnitTests/Puntiro.UnitTests.csproj --configuration Release --filter FullyQualifiedName~Tenancy
```

Expected: FAIL because Tenancy domain types do not exist.

- [ ] **Step 2: Implement the domain and public contracts**

Use exact states:

```csharp
public enum OrganizationStatus { Provisioning, Active, Suspended }
public enum MembershipRole { Owner }
public enum MembershipStatus { Active, Revoked }

public sealed record OrganizationSnapshot(
    Guid Id, string DisplayName, string Slug, OrganizationStatus Status, long Version);
public sealed record MembershipSnapshot(
    Guid Id, Guid OrganizationId, Guid UserId, MembershipRole Role, MembershipStatus Status, long Version);
public sealed record TenantAccess(Guid OrganizationId, Guid UserId, MembershipRole Role);
```

Keep transitions on domain methods. `Activate` accepts only a verified active-owner condition, `Suspend` never deletes membership, and `EnsureOwnerMembershipAsync` is idempotent for the same `(organizationId,userId)`.

- [ ] **Step 3: Record PostgreSQL RED and implement owned schema**

`PostgresDatabase` must require `PUNTIRO_TEST_POSTGRES`, create a uniquely named disposable database from the maintenance connection, and drop it after the test collection. Do not silently skip when the variable is absent.

Write a real integration test:

```csharp
[Fact]
public async Task Slug_and_membership_constraints_are_enforced_by_postgres()
{
    await using var scope = await TenancyTestScope.CreateAsync(_database.ConnectionString);
    var first = await scope.Service.GetOrCreateProvisioningAsync("Puntiro", "puntiro", default);
    var repeated = await scope.Service.GetOrCreateProvisioningAsync("Puntiro", "PUNTIRO", default);
    Assert.Equal(first.Id, repeated.Id);

    var membership = await scope.Service.EnsureOwnerMembershipAsync(first.Id, UserId, default);
    var duplicate = await scope.Service.EnsureOwnerMembershipAsync(first.Id, UserId, default);
    Assert.Equal(membership.Id, duplicate.Id);
}
```

Map schema `tenancy`, unique `organizations.slug`, unique `(organization_id,user_id)`, append-only `security_events`, UTC timestamps and `Version` as an optimistic concurrency token. Organization activation appends a redacted event in the same Tenancy transaction. Generate the migration with the pinned tool, normalize its ID to `202608080001_InitialTenancy`, and inspect it to ensure it touches only schema `tenancy`:

```bash
dotnet ef migrations add InitialTenancy --project src/Puntiro.Modules.Tenancy --startup-project apps/cloud --context TenancyDbContext --output-dir Persistence/Migrations
```

`TenancyDbContextFactory` reads only `ConnectionStrings__Puntiro`; it has no checked-in fallback credential.

- [ ] **Step 4: Run unit and PostgreSQL GREEN**

```bash
dotnet test tests/Puntiro.UnitTests/Puntiro.UnitTests.csproj --configuration Release --filter FullyQualifiedName~Tenancy
dotnet test tests/Puntiro.IntegrationTests/Puntiro.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~Tenancy
```

Expected: lifecycle, uniqueness, idempotency and real migration tests PASS.

- [ ] **Step 5: Document and commit Tenancy**

In `docs/modules/tenancy.md`, document owned tables, application contracts, lifecycle, uniqueness, no direct Identity reads and failure modes.

```bash
git add src/Puntiro.Modules.Tenancy tests/Puntiro.UnitTests/Tenancy tests/Puntiro.IntegrationTests/Infrastructure tests/Puntiro.IntegrationTests/Tenancy docs/modules/tenancy.md
git commit -m "feat: add tenancy lifecycle"
```

### Task 3: Implement Identity Cryptographic Primitives

**Files:**
- Create: `src/Puntiro.Security/ISecretGenerator.cs`
- Create: `src/Puntiro.Security/SystemSecretGenerator.cs`
- Create: `src/Puntiro.Security/Base32.cs`
- Create: `src/Puntiro.Security/Base64Url.cs`
- Create: `src/Puntiro.Security/SensitiveValue.cs`
- Create: `src/Puntiro.Modules.Identity/Security/EmailAddress.cs`
- Create: `src/Puntiro.Modules.Identity/Security/PasswordHasher.cs`
- Create: `src/Puntiro.Modules.Identity/Security/PasswordPolicy.cs`
- Create: `src/Puntiro.Modules.Identity/Security/Rfc6238Totp.cs`
- Create: `src/Puntiro.Modules.Identity/Security/RecoveryCodeService.cs`
- Create: `tests/Puntiro.UnitTests/Security/TestSecretGenerator.cs`
- Create: `tests/Puntiro.UnitTests/Identity/EmailAddressTests.cs`
- Create: `tests/Puntiro.UnitTests/Identity/PasswordHasherTests.cs`
- Create: `tests/Puntiro.UnitTests/Identity/Rfc6238TotpTests.cs`
- Create: `tests/Puntiro.UnitTests/Identity/RecoveryCodeServiceTests.cs`

**Interfaces:**
- Consumes: `Konscious.Security.Cryptography.Argon2`, BCL cryptography and injected `TimeProvider`/`ISecretGenerator`.
- Produces:

```csharp
public sealed record PasswordHash(
    byte[] Salt, byte[] Hash, int MemoryKiB, int Iterations, int Parallelism, string Algorithm);
public enum PasswordVerification { Failed, Valid, ValidNeedsRehash }

internal interface IPasswordHasher
{
    Task<PasswordHash> HashAsync(string password, CancellationToken cancellationToken);
    Task<PasswordVerification> VerifyAsync(
        string password, PasswordHash stored, CancellationToken cancellationToken);
}

internal readonly record struct AcceptedTotp(long Counter);
```

- [ ] **Step 1: Write official-vector and policy RED tests**

Use RFC 6238 Appendix B vectors for HMAC-SHA-1 and add these product assertions:

```csharp
[Theory]
[InlineData(59L, "94287082")]
[InlineData(1111111109L, "07081804")]
[InlineData(1111111111L, "14050471")]
[InlineData(1234567890L, "89005924")]
[InlineData(2000000000L, "69279037")]
[InlineData(20000000000L, "65353130")]
public void Matches_rfc6238_sha1_vectors(long unixSeconds, string expected) =>
    Assert.Equal(expected, Rfc6238Totp.Generate(RfcSha1Secret, unixSeconds, digits: 8));

[Fact]
public async Task Hash_uses_exact_argon2id_policy_and_unique_salt()
{
    var first = await _hasher.HashAsync("correct horse battery staple", default);
    var second = await _hasher.HashAsync("correct horse battery staple", default);
    Assert.Equal((19456, 2, 1, "argon2id"),
        (first.MemoryKiB, first.Iterations, first.Parallelism, first.Algorithm));
    Assert.Equal(16, first.Salt.Length);
    Assert.Equal(32, first.Hash.Length);
    Assert.NotEqual(first.Salt, second.Salt);
}

[Fact]
public void Totp_rejects_the_last_accepted_counter() =>
    Assert.False(_totp.TryAccept(Secret, CurrentCode, Now, lastAcceptedCounter: CurrentCounter,
        out _));
```

Run:

```bash
dotnet test tests/Puntiro.UnitTests/Puntiro.UnitTests.csproj --configuration Release --filter "FullyQualifiedName~Identity|FullyQualifiedName~Security"
```

Expected: FAIL on missing crypto services.

- [ ] **Step 2: Implement bounded password and normalization policy**

`EmailAddress.Normalize` trims, NFC-normalizes, requires one `@`, applies IDNA/lower-case to the domain and invariant lower-case to the local part; reject normalized values above 320 UTF-8 bytes. Preserve the original trimmed display email separately.

Password policy allows all Unicode without composition rules, requires 12–128 Unicode scalar values, rejects inputs above 1024 UTF-8 bytes before Argon2 allocation, uses a unique 16-byte salt and emits a 32-byte Argon2id hash. Verify stored parameters, return `ValidNeedsRehash` for weaker values, and use `CryptographicOperations.FixedTimeEquals`.

- [ ] **Step 3: Implement RFC 6238 and recovery primitives**

`Rfc6238Totp` must calculate counters from injected UTC, check counters in deterministic order `current`, `previous`, `next`, and reject any accepted counter `<= lastAcceptedCounter`. It must never log the code or secret.

Generate a 20-byte TOTP secret. Generate ten 16-byte recovery values, encode them with uppercase Base32 and stable grouping, normalize by removing hyphens, and verify only an HMAC-SHA-256 digest using the recovery key purpose.

`SensitiveValue.ToString()` must always return `[REDACTED]`; only `Reveal()` exposes the value to the narrowly scoped response/cookie boundary.

- [ ] **Step 4: Run focused GREEN and allocation/error cases**

```bash
dotnet test tests/Puntiro.UnitTests/Puntiro.UnitTests.csproj --configuration Release --filter "FullyQualifiedName~Identity|FullyQualifiedName~Security"
dotnet build src/Puntiro.Modules.Identity/Puntiro.Modules.Identity.csproj --configuration Release
```

Expected: RFC vectors, replay, password policy, rehash, email normalization, unique salts and recovery one-time primitives PASS.

- [ ] **Step 5: Commit cryptographic primitives**

```bash
git add src/Puntiro.Security src/Puntiro.Modules.Identity/Security tests/Puntiro.UnitTests/Security tests/Puntiro.UnitTests/Identity
git commit -m "feat: add identity cryptographic primitives"
```

### Task 4: Persist Identity, Authenticate Factors and Manage Sessions

**Files:**
- Create: `src/Puntiro.Modules.Identity/Contracts/IIdentityProvisioningService.cs`
- Create: `src/Puntiro.Modules.Identity/Contracts/IAdminAuthenticationService.cs`
- Create: `src/Puntiro.Modules.Identity/Contracts/IAdminSessionService.cs`
- Create: `src/Puntiro.Modules.Identity/Domain/AdminUser.cs`
- Create: `src/Puntiro.Modules.Identity/Domain/IdentitySecurityEvent.cs`
- Create: `src/Puntiro.Modules.Identity/Persistence/IdentityDbContext.cs`
- Create: `src/Puntiro.Modules.Identity/Persistence/IdentityDbContextFactory.cs`
- Create: `src/Puntiro.Modules.Identity/Persistence/IdentityModelConfiguration.cs`
- Create: `src/Puntiro.Modules.Identity/Persistence/Migrations/202608080002_InitialIdentity.cs`
- Create: `src/Puntiro.Modules.Identity/Persistence/Migrations/202608080002_InitialIdentity.Designer.cs`
- Create: `src/Puntiro.Modules.Identity/Persistence/Migrations/IdentityDbContextModelSnapshot.cs`
- Create: `src/Puntiro.Modules.Identity/Security/IdentityKeyOptions.cs`
- Create: `src/Puntiro.Modules.Identity/Security/TotpSecretProtector.cs`
- Create: `src/Puntiro.Modules.Identity/Services/IdentityProvisioningService.cs`
- Create: `src/Puntiro.Modules.Identity/Services/AdminAuthenticationService.cs`
- Create: `src/Puntiro.Modules.Identity/Services/AdminSessionService.cs`
- Create: `src/Puntiro.Modules.Identity/IdentityModule.cs`
- Create: `tests/Puntiro.UnitTests/Identity/SessionTokenTests.cs`
- Create: `tests/Puntiro.UnitTests/Identity/SessionExpiryTests.cs`
- Create: `tests/Puntiro.IntegrationTests/Identity/IdentityPersistenceTests.cs`
- Create: `tests/Puntiro.IntegrationTests/Identity/IdentityAuthenticationTests.cs`
- Create: `tests/Puntiro.IntegrationTests/Identity/IdentitySessionTests.cs`
- Create: `docs/modules/identity.md`

**Interfaces:**
- Consumes: Task 3 crypto primitives, Data Protection, `TimeProvider`, `ISecretGenerator`, identity schema.
- Produces:

```csharp
public enum VerifiedFactor { Totp, RecoveryCode }
public sealed record PendingOwnerIdentity(
    Guid UserId, string DisplayEmail, SensitiveValue TotpUri,
    IReadOnlyList<SensitiveValue> RecoveryCodes);
public sealed record VerifiedIdentity(Guid UserId, VerifiedFactor Factor, DateTimeOffset VerifiedAt);
public sealed class AdminCredentials
{
    public AdminCredentials(string email, string password, string? totpCode, string? recoveryCode) =>
        (Email, Password, TotpCode, RecoveryCode) = (email, password, totpCode, recoveryCode);
    public string Email { get; }
    public string Password { get; }
    public string? TotpCode { get; }
    public string? RecoveryCode { get; }
    public override string ToString() => nameof(AdminCredentials);
}
public sealed record IssuedAdminSession(
    AdminSessionPrincipal Principal, SensitiveValue RawToken);
public sealed class PendingOwnerTotpReset
{
    public required Guid UserId { get; init; }
    public required SensitiveValue TotpUri { get; init; }
    public required IReadOnlyList<SensitiveValue> RecoveryCodes { get; init; }
    // Candidate secret, recovery values and the verified old recovery-row version
    // remain internal to the Identity assembly on this opaque object.
}
public sealed record AdminSessionPrincipal(
    Guid SessionId, Guid UserId, Guid OrganizationId,
    DateTimeOffset IdleExpiresAt, DateTimeOffset AbsoluteExpiresAt,
    DateTimeOffset? SecondFactorVerifiedAt);

public interface IIdentityProvisioningService
{
    Task<PendingOwnerIdentity> BeginOwnerAsync(
        Guid provisioningOrganizationId, string email, string password,
        CancellationToken cancellationToken);
    Task ConfirmOwnerTotpAsync(Guid userId, string code, CancellationToken cancellationToken);
    Task CompleteOwnerAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken);
    Task<PendingOwnerTotpReset> PrepareOwnerTotpResetAsync(
        Guid userId, string password, string recoveryCode, CancellationToken cancellationToken);
    Task CompleteOwnerTotpResetAsync(
        PendingOwnerTotpReset pending, string firstTotpCode, CancellationToken cancellationToken);
}

public interface IAdminAuthenticationService
{
    Task<VerifiedIdentity?> VerifyAsync(AdminCredentials credentials, CancellationToken cancellationToken);
    Task<DateTimeOffset?> StepUpTotpAsync(Guid userId, string code, CancellationToken cancellationToken);
}

public interface IAdminSessionService
{
    Task<IssuedAdminSession> CreateAsync(
        VerifiedIdentity identity, Guid organizationId, CancellationToken cancellationToken);
    Task<AdminSessionPrincipal?> ValidateAsync(string presentedToken, CancellationToken cancellationToken);
    Task RevokeAsync(Guid sessionId, string reason, CancellationToken cancellationToken);
    Task RevokeAllForUserAsync(Guid userId, string reason, CancellationToken cancellationToken);
}
```

- [ ] **Step 1: Write session and factor RED tests**

```csharp
[Fact]
public async Task Recovery_login_does_not_grant_fresh_step_up()
{
    var verified = await _authentication.VerifyAsync(
        new AdminCredentials(Email, Password, TotpCode: null, RecoveryCode), default);
    var session = await _sessions.CreateAsync(verified!, OrganizationId, default);
    Assert.Null(session.Principal.SecondFactorVerifiedAt);
}

[Theory]
[InlineData(29, true)]
[InlineData(30, false)]
public async Task Session_honors_idle_timeout(int inactiveMinutes, bool expectedValid)
{
    _time.SetUtcNow(CreatedAt.AddMinutes(inactiveMinutes));
    Assert.Equal(expectedValid, await _sessions.ValidateAsync(RawSession, default) is not null);
}
```

Also test 12-hour absolute expiry, revoke, wrong HMAC, unknown email dummy Argon path, TOTP replay and atomic recovery use.

Run:

```bash
dotnet test tests/Puntiro.UnitTests/Puntiro.UnitTests.csproj --configuration Release --filter FullyQualifiedName~Session
dotnet test tests/Puntiro.IntegrationTests/Puntiro.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~Identity
```

Expected: FAIL because Identity persistence/services are missing.

- [ ] **Step 2: Map Identity-owned tables and migration**

Use schema `identity` and tables `admin_users`, `password_credentials`, `totp_credentials`, `recovery_codes`, `sessions`, `security_events`. Enforce unique `normalized_email`, one password/TOTP per user, unique session public ID, indexes for expiry/revoke, and `Version` concurrency tokens. `admin_users.status` is `provisioning|active|suspended` and stores nullable `provisioning_organization_id` for safe bootstrap resume.

Encrypt the raw TOTP bytes before EF sees the property, using an `IDataProtector` purpose containing `Puntiro.Identity.Totp.v1` and user ID. Store only recovery HMAC values and key versions.

Generate the context migration with pinned `dotnet-ef`, normalize its ID to `202608080002_InitialIdentity`, and keep its history table in schema `identity`. `IdentityDbContextFactory` requires `ConnectionStrings__Puntiro` and has no fallback credential.

- [ ] **Step 3: Implement authentication and session transactions**

For unknown/suspended users, execute the same current Argon2id work against a fixed dummy credential before returning the generic failure. Verify exactly one of TOTP/recovery. In one identity transaction, update accepted TOTP counter or consume a recovery row and append a redacted security event.

Session token format is `pns_<public-id>.<base64url-secret>`. Store only HMAC-SHA-256 over purpose + public ID + secret with a versioned session key. Create 30-minute idle and 12-hour absolute expiry. Coalesce `last_seen_at` writes to at most once per five minutes without allowing a request at or after either expiry boundary.

Successful TOTP login copies `VerifiedAt` to `second_factor_verified_at`; recovery login stores null. `StepUpTotpAsync` uses the same replay-protected TOTP transaction.

- [ ] **Step 4: Run real PostgreSQL and concurrency GREEN**

```bash
dotnet test tests/Puntiro.UnitTests/Puntiro.UnitTests.csproj --configuration Release --filter "FullyQualifiedName~Identity|FullyQualifiedName~Session"
dotnet test tests/Puntiro.IntegrationTests/Puntiro.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~Identity
```

Expected: unique email, encrypted TOTP persistence, atomic replay/recovery, generic failure, session HMAC, idle/absolute expiry and revoke tests PASS.

- [ ] **Step 5: Document and commit Identity**

Document table ownership, Argon policy, factor semantics, session expiry, key purposes and failure modes in `docs/modules/identity.md`.

```bash
git add src/Puntiro.Modules.Identity tests/Puntiro.UnitTests/Identity tests/Puntiro.IntegrationTests/Identity docs/modules/identity.md
git commit -m "feat: add identity and server sessions"
```

### Task 5: Build the Interactive Provisioning and TOTP Recovery CLI

**Files:**
- Create: `tools/Puntiro.Provisioning/Cli/ProvisioningArguments.cs`
- Create: `tools/Puntiro.Provisioning/Cli/IProvisioningTerminal.cs`
- Create: `tools/Puntiro.Provisioning/Cli/SystemProvisioningTerminal.cs`
- Create: `tools/Puntiro.Provisioning/Bootstrap/BootstrapOwnerOrchestrator.cs`
- Create: `tools/Puntiro.Provisioning/Recovery/ResetOwnerTotpOrchestrator.cs`
- Modify: `tools/Puntiro.Provisioning/Program.cs`
- Create: `tests/Puntiro.UnitTests/Provisioning/ProvisioningArgumentsTests.cs`
- Create: `tests/Puntiro.IntegrationTests/Provisioning/BootstrapOwnerTests.cs`
- Create: `tests/Puntiro.IntegrationTests/Provisioning/ResetOwnerTotpTests.cs`
- Create: `docs/runbooks/first-owner-provisioning.md`
- Create: `docs/runbooks/owner-totp-recovery.md`

**Interfaces:**
- Consumes: `ITenancyProvisioningService`, `ITenantAccessService`, `IIdentityProvisioningService`, `IAdminAuthenticationService`, `IAdminSessionService`.
- Produces two non-HTTP commands:

```text
Puntiro.Provisioning bootstrap-owner --organization-name <name> --organization-slug <slug> --email <email>
Puntiro.Provisioning reset-owner-totp --organization-slug <slug> --email <email>
```

```csharp
public enum ProvisioningExit
{
    Success = 0,
    InvalidArguments = 2,
    Conflict = 3,
    InvalidCredentials = 4,
    InfrastructureFailure = 5,
}

public sealed record ProvisioningParseResult(
    ProvisioningExit ExitCode, string? Command, IReadOnlyDictionary<string, string> Arguments);
```

- [ ] **Step 1: Write CLI parsing and bootstrap RED tests**

Assert that no password, TOTP or recovery argument exists:

```csharp
[Theory]
[InlineData("--password")]
[InlineData("--totp")]
[InlineData("--recovery-code")]
public void Secret_options_are_rejected(string option)
{
    var result = ProvisioningArguments.Parse(["bootstrap-owner", option, "secret"]);
    Assert.Equal(ProvisioningExit.InvalidArguments, result.ExitCode);
}
```

Add a real PostgreSQL orchestration test that captures terminal secrets in memory and verifies `organization.Status == Active` only after a valid first TOTP and active owner membership. Add an interrupted-run test that stops after URI output, reruns, and verifies the pending credential/recovery batch is replaced without duplicate organization, user or membership.

Run:

```bash
dotnet test tests/Puntiro.UnitTests/Puntiro.UnitTests.csproj --configuration Release --filter FullyQualifiedName~Provisioning
dotnet test tests/Puntiro.IntegrationTests/Puntiro.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~Provisioning
```

Expected: FAIL because CLI parser and orchestrators do not exist.

- [ ] **Step 2: Implement a strict interactive terminal**

`SystemProvisioningTerminal` must:

- reject redirected input or output for commands that reveal secrets;
- read password/recovery/TOTP with `Console.ReadKey(intercept: true)` and zero mutable buffers after use where possible;
- emit TOTP URI and recovery codes only through the dedicated secret-output method;
- never pass secret values to `ILogger`, exceptions or command result objects;
- return stable non-secret exit codes for invalid arguments, conflict, invalid credentials and infrastructure failure.

- [ ] **Step 3: Implement idempotent bootstrap**

The orchestrator performs the approved sequence:

```csharp
var organization = await tenancy.GetOrCreateProvisioningAsync(name, slug, cancellationToken);
var password = await terminal.ReadPasswordAsync(cancellationToken);
var pending = await identity.BeginOwnerAsync(organization.Id, email, password, cancellationToken);
await terminal.WriteEnrollmentAsync(pending.TotpUri, pending.RecoveryCodes, cancellationToken);
var firstCode = await terminal.ReadTotpAsync(cancellationToken);
await identity.ConfirmOwnerTotpAsync(pending.UserId, firstCode, cancellationToken);
await tenancy.EnsureOwnerMembershipAsync(organization.Id, pending.UserId, cancellationToken);
await tenancy.ActivateAsync(organization.Id, cancellationToken);
await identity.CompleteOwnerAsync(pending.UserId, organization.Id, cancellationToken);
```

An active organization exits without mutation. Existing identity can resume only when `provisioning_organization_id` matches. A mismatch is a hard conflict.

If activation succeeded but `CompleteOwnerAsync` was interrupted, a retry verifies the same active owner/membership and performs only the safe correlation cleanup; it never rotates an active owner's credentials or creates a second membership.

- [ ] **Step 4: Implement owner TOTP recovery**

`reset-owner-totp` requires active owner membership, hidden password and hidden unused recovery code. Keep the new TOTP secret in memory until the operator enters a valid first code. One identity transaction then replaces the TOTP credential and recovery batch, consumes/invalidates every old recovery code, revokes every existing session and appends a redacted security event. A failed new TOTP verification leaves stored credentials unchanged.

Call `PrepareOwnerTotpResetAsync`, display only its `TotpUri` and `RecoveryCodes`, read the first new code, then call `CompleteOwnerTotpResetAsync`. Completion rechecks that the old recovery row captured by the opaque candidate is still unused before committing.

- [ ] **Step 5: Run GREEN and prove output redaction**

```bash
dotnet test tests/Puntiro.UnitTests/Puntiro.UnitTests.csproj --configuration Release --filter FullyQualifiedName~Provisioning
dotnet test tests/Puntiro.IntegrationTests/Puntiro.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~Provisioning
```

Expected: bootstrap/resume/conflict/recovery/session-revoke tests PASS and captured logs contain none of the fixture secrets.

- [ ] **Step 6: Document and commit provisioning**

The runbooks must contain exact commands, prerequisites, interruption semantics, safe terminal requirements, recovery behavior and explicit warnings against shell history/output capture. Examples use dummy identifiers and never a valid credential.

```bash
git add tools/Puntiro.Provisioning tests/Puntiro.UnitTests/Provisioning tests/Puntiro.IntegrationTests/Provisioning docs/runbooks/first-owner-provisioning.md docs/runbooks/owner-totp-recovery.md
git commit -m "feat: add owner provisioning cli"
```

### Task 6: Issue, Authenticate and Revoke Integration Tokens

**Files:**
- Create: `src/Puntiro.Modules.Integrations/Contracts/IIntegrationTokenService.cs`
- Create: `src/Puntiro.Modules.Integrations/Domain/IntegrationScope.cs`
- Create: `src/Puntiro.Modules.Integrations/Domain/IntegrationToken.cs`
- Create: `src/Puntiro.Modules.Integrations/Domain/IntegrationSecurityEvent.cs`
- Create: `src/Puntiro.Modules.Integrations/Persistence/IntegrationsDbContext.cs`
- Create: `src/Puntiro.Modules.Integrations/Persistence/IntegrationsDbContextFactory.cs`
- Create: `src/Puntiro.Modules.Integrations/Persistence/IntegrationsModelConfiguration.cs`
- Create: `src/Puntiro.Modules.Integrations/Persistence/Migrations/202608080003_InitialIntegrations.cs`
- Create: `src/Puntiro.Modules.Integrations/Persistence/Migrations/202608080003_InitialIntegrations.Designer.cs`
- Create: `src/Puntiro.Modules.Integrations/Persistence/Migrations/IntegrationsDbContextModelSnapshot.cs`
- Create: `src/Puntiro.Modules.Integrations/Security/IntegrationTokenCodec.cs`
- Create: `src/Puntiro.Modules.Integrations/Security/IntegrationKeyOptions.cs`
- Create: `src/Puntiro.Modules.Integrations/Services/IntegrationTokenService.cs`
- Create: `src/Puntiro.Modules.Integrations/IntegrationsModule.cs`
- Create: `tests/Puntiro.UnitTests/Integrations/IntegrationTokenCodecTests.cs`
- Create: `tests/Puntiro.UnitTests/Integrations/IntegrationScopeTests.cs`
- Create: `tests/Puntiro.IntegrationTests/Integrations/IntegrationTokenPersistenceTests.cs`
- Create: `tests/Puntiro.IntegrationTests/Integrations/IntegrationTokenServiceTests.cs`
- Create: `docs/modules/integrations.md`

**Interfaces:**
- Consumes: active organization/owner checks supplied by Cloud, `ISecretGenerator`, `TimeProvider`, versioned integration HMAC keys.
- Produces:

```csharp
public enum IntegrationScope { ShipmentsRead, ShipmentsWrite }
public sealed record CreateIntegrationToken(
    Guid OrganizationId, Guid CreatedByUserId, string DisplayName,
    IReadOnlySet<IntegrationScope> Scopes);
public sealed record IntegrationTokenMetadata(
    Guid Id, string PublicId, string DisplayName,
    IReadOnlySet<IntegrationScope> Scopes, DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt, DateTimeOffset? RevokedAt, long Version);
public sealed record IssuedIntegrationToken(
    IntegrationTokenMetadata Metadata, SensitiveValue RawToken);
public sealed record IntegrationPrincipal(
    Guid TokenId, Guid OrganizationId, IReadOnlySet<IntegrationScope> Scopes);
public sealed class ActiveTokenLimitException : Exception { }

public interface IIntegrationTokenService
{
    Task<IssuedIntegrationToken> CreateAsync(CreateIntegrationToken command, CancellationToken cancellationToken);
    Task<IReadOnlyList<IntegrationTokenMetadata>> ListAsync(Guid organizationId, CancellationToken cancellationToken);
    Task RevokeAsync(Guid organizationId, Guid tokenId, Guid revokedByUserId, long expectedVersion, CancellationToken cancellationToken);
    Task<IntegrationPrincipal?> AuthenticateAsync(string presentedToken, CancellationToken cancellationToken);
}
```

- [ ] **Step 1: Write token-format and service RED tests**

```csharp
[Fact]
public async Task Create_returns_secret_once_but_persists_only_verifier()
{
    var issued = await _service.CreateAsync(Command, default);
    Assert.Matches("^pnt_live_[A-Za-z0-9_-]+\\.[A-Za-z0-9_-]+$", issued.RawToken.Reveal());
    var row = await _db.IntegrationTokens.SingleAsync();
    Assert.DoesNotContain(issued.RawToken.Reveal(), _db.ChangeTracker.DebugView.LongView);
    Assert.Equal(32, row.SecretVerifier.Length);
}

[Fact]
public async Task Third_active_token_is_rejected()
{
    await _service.CreateAsync(First, default);
    await _service.CreateAsync(Second, default);
    await Assert.ThrowsAsync<ActiveTokenLimitException>(() => _service.CreateAsync(Third, default));
}
```

Also cover invalid prefix, malformed Base64Url, wrong secret, unknown public ID, constant-time verifier boundary, empty/unknown scopes, immediate revoke and cross-organization revoke concealment.

Run focused unit and PostgreSQL tests; expect missing-type FAIL.

- [ ] **Step 2: Implement token codec and owned schema**

Generate a random public ID and independent 32-byte secret. Encode Base64Url without padding. HMAC input must bind the fixed purpose, public ID and secret. Store only verifier plus key version. Parse into bounded spans before any database query and return null for malformed input.

Map schema `integrations`, unique public ID, organization/revoked indexes, normalized scope rows or a validated PostgreSQL representation, timestamps and optimistic `Version`. Migration must touch only `integrations`.

Generate with pinned `dotnet-ef`, normalize the migration ID to `202608080003_InitialIntegrations`, and keep the history table in schema `integrations`. `IntegrationsDbContextFactory` requires `ConnectionStrings__Puntiro` and has no fallback credential.

- [ ] **Step 3: Implement creation, list, authentication and revoke**

Validate display name as trimmed 1–100 Unicode scalar values. Require one or both approved scopes. Count active organization tokens inside a serializable creation transaction with retry on PostgreSQL serialization failure, and reject the third. Add a concurrent integration test starting from one active token: exactly one of two simultaneous creates succeeds and the final active count is two. Return raw token only from create. List returns metadata only. Revoke is idempotent for the same organization/token and returns not-found semantics for a foreign organization.

On successful authentication, return organization/scopes from the verified row. Coalesce `last_used_at` writes to one per 15 minutes. A revoked token fails immediately even if its HMAC is valid.

- [ ] **Step 4: Run GREEN and declaration/log scans**

```bash
dotnet test tests/Puntiro.UnitTests/Puntiro.UnitTests.csproj --configuration Release --filter FullyQualifiedName~Integrations
dotnet test tests/Puntiro.IntegrationTests/Puntiro.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~Integrations
rg -n "RawToken|SecretVerifier" src/Puntiro.Modules.Integrations
```

Expected: token/scopes/limit/revoke/tenant tests PASS; every raw-token occurrence is confined to issuance/HTTP response code and no logger template accepts it.

- [ ] **Step 5: Document and commit Integrations**

```bash
git add src/Puntiro.Modules.Integrations tests/Puntiro.UnitTests/Integrations tests/Puntiro.IntegrationTests/Integrations docs/modules/integrations.md
git commit -m "feat: add integration token lifecycle"
```

### Task 7: Expose Secure Admin Authentication HTTP Endpoints

**Files:**
- Create: `apps/cloud/Configuration/CloudSecurityOptions.cs`
- Create: `apps/cloud/Configuration/CloudServiceCollectionExtensions.cs`
- Create: `apps/cloud/Auth/AdminSessionAuthenticationHandler.cs`
- Create: `apps/cloud/Auth/OwnerAuthorizationHandler.cs`
- Create: `apps/cloud/Http/TenantContext.cs`
- Create: `apps/cloud/Http/ApiProblem.cs`
- Create: `apps/cloud/Http/GlobalExceptionHandler.cs`
- Create: `apps/cloud/Http/SensitiveBodyRedaction.cs`
- Create: `apps/cloud/OpenApi/AuthenticationDocumentTransformer.cs`
- Create: `apps/cloud/OpenApi/AuthenticationOperationTransformer.cs`
- Create: `apps/cloud/Endpoints/AdminAuthEndpoints.cs`
- Create: `apps/cloud/Health/CloudReadinessHealthCheck.cs`
- Create: `apps/cloud/appsettings.json`
- Modify: `apps/cloud/Program.cs`
- Create: `tests/Puntiro.IntegrationTests/Cloud/CloudWebApplicationFactory.cs`
- Create: `tests/Puntiro.IntegrationTests/Cloud/ApiProblemResponse.cs`
- Create: `tests/Puntiro.IntegrationTests/Cloud/AdminAuthApiTests.cs`
- Create: `tests/Puntiro.IntegrationTests/Cloud/AdminSessionApiTests.cs`
- Create: `tests/Puntiro.IntegrationTests/Cloud/CloudHealthTests.cs`
- Create: `tests/Puntiro.IntegrationTests/Cloud/LogRedactionTests.cs`
- Create: `tests/Puntiro.IntegrationTests/Cloud/OpenApiContractTests.cs`

**Interfaces:**
- Consumes: Identity authentication/session contracts and Tenancy active-membership contracts.
- Produces routes:

```text
POST /api/admin/auth/login
POST /api/admin/auth/logout
GET  /api/admin/auth/session
POST /api/admin/auth/step-up
GET  /health/live
GET  /health/ready
GET  /openapi/v1.json
```

- [ ] **Step 1: Write end-to-end HTTP RED tests**

```csharp
[Fact]
public async Task Login_sets_only_the_approved_cookie_and_generic_failure()
{
    var success = await Client.PostAsJsonAsync("/api/admin/auth/login",
        new { email = OwnerEmail, password = OwnerPassword, totpCode = CurrentTotp });
    var setCookie = success.Headers.GetValues("Set-Cookie").Single();
    Assert.Contains("__Host-puntiro_session=", setCookie);
    Assert.Contains("Secure", setCookie);
    Assert.Contains("HttpOnly", setCookie);
    Assert.Contains("SameSite=Strict", setCookie);
    Assert.DoesNotContain("Domain=", setCookie);

    var unknown = await Client.PostAsJsonAsync("/api/admin/auth/login",
        new { email = "missing@example.test", password = "wrong password", totpCode = "000000" });
    var wrongPassword = await Client.PostAsJsonAsync("/api/admin/auth/login",
        new { email = OwnerEmail, password = "wrong password", totpCode = "000000" });
    var unknownProblem = await unknown.Content.ReadFromJsonAsync<ApiProblemResponse>();
    var wrongProblem = await wrongPassword.Content.ReadFromJsonAsync<ApiProblemResponse>();
    Assert.Equal((401, "auth.invalid_credentials"),
        ((int)unknown.StatusCode, unknownProblem!.Code));
    Assert.Equal((unknownProblem.Status, unknownProblem.Code, unknownProblem.Title),
        (wrongProblem!.Status, wrongProblem.Code, wrongProblem.Title));
}
```

Add tests for exactly one second-factor field, recovery login without step-up, single owner membership selection, explicit `409 auth.organization_selection_required` for multiple memberships, idle/absolute expiry, logout, CSRF rejection, same-origin policy, TOTP step-up freshness, login/step-up rate limits, readiness and secret-free captured logs.

The OpenAPI test requires document `v1`, `application/problem+json` error responses, the `AdminSession` cookie scheme, operation IDs and no password/TOTP/recovery examples.

`CloudWebApplicationFactory` uses `https://localhost` as the client base address so Secure-cookie behavior is exercised, injects a temporary Data Protection certificate/key directory, and applies all three migrations to its disposable PostgreSQL database before the first request.

Run:

```bash
dotnet test tests/Puntiro.IntegrationTests/Puntiro.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~Cloud
```

Expected: FAIL because Cloud routes and auth handlers do not exist.

- [ ] **Step 2: Compose modules and validate configuration at startup**

Register all module contexts with `UseNpgsql`, explicit schema/migrations history tables, retry policy only outside tests, shared `TimeProvider.System`, Data Protection application name `Puntiro.Cloud`, persistent key path and independent versioned HMAC options.

Required configuration names:

```text
ConnectionStrings__Puntiro
Puntiro__Security__DataProtectionKeysPath
Puntiro__Security__DataProtectionCertificatePath
Puntiro__Security__DataProtectionCertificatePassword
Puntiro__Security__SessionHmac__CurrentVersion
Puntiro__Security__SessionHmac__Keys__v1
Puntiro__Security__RecoveryHmac__CurrentVersion
Puntiro__Security__RecoveryHmac__Keys__v1
Puntiro__Security__IntegrationHmac__CurrentVersion
Puntiro__Security__IntegrationHmac__Keys__v1
```

Use `ValidateOnStart`; production startup fails on missing/short/invalid keys, missing persistent key path or missing key-protection certificate. Development and test factories may supply a temporary certificate explicitly. `appsettings.json` contains timeouts and non-secret defaults only.

- [ ] **Step 3: Implement cookie authentication, tenant context and CSRF**

The admin handler reads only `__Host-puntiro_session`, calls `ValidateAsync`, and emits user/session/organization claims from the validated server record. Owner authorization rechecks active membership through Tenancy before the endpoint delegate runs. A scoped `TenantContext` is built from these trusted claims; request body/query/header organization identifiers never populate it.

Issue an ASP.NET antiforgery token from `GET /session`; require its header on logout, step-up and every future state-changing Admin route. Validate allowed origin against the configured Admin origin as defense in depth.

- [ ] **Step 4: Implement endpoints, problems and rate limits**

Use classes whose `ToString()` is always redacted:

```csharp
public sealed class AdminLoginRequest
{
    public required string Email { get; init; }
    public required string Password { get; init; }
    public string? TotpCode { get; init; }
    public string? RecoveryCode { get; init; }
    public override string ToString() => nameof(AdminLoginRequest);
}
public sealed class StepUpRequest
{
    public required string TotpCode { get; init; }
    public override string ToString() => nameof(StepUpRequest);
}
public sealed record AdminSessionResponse(
    Guid UserId, string Email, Guid OrganizationId, string Role,
    DateTimeOffset IdleExpiresAt, DateTimeOffset AbsoluteExpiresAt,
    DateTimeOffset? SecondFactorVerifiedAt, string AntiforgeryToken);
```

Success statuses are fixed: login `204`, logout `204`, session `200`, step-up `204`. Invalid request shape is `400`, generic credential failure/session expiry is `401`, authorization/step-up failure is `403`, and rate limiting is `429` with `Retry-After`.

Use stable problem codes `auth.invalid_credentials`, `auth.session_expired`, `auth.forbidden`, `auth.csrf_invalid`, `auth.rate_limited`, `auth.organization_selection_required`, `configuration.not_ready`. Login has a per-IP limiter and a keyed normalized-email limiter; step-up has a per-session limiter. Hash email partitions with a process-random 32-byte key created at startup, never with a persisted credential key and never with raw email. Do not enable request-body logging on auth routes.

- [ ] **Step 5: Implement readiness without runtime migration**

`/health/ready` opens PostgreSQL, asks every context for pending migrations without applying them, and verifies Data Protection directory plus all current HMAC keys. It returns unhealthy on a pending migration. Search production startup and assert no `Migrate`, `MigrateAsync` or `EnsureCreated` call exists.

- [ ] **Step 6: Run HTTP GREEN and commit**

```bash
dotnet test tests/Puntiro.IntegrationTests/Puntiro.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~Cloud
dotnet build apps/cloud/Puntiro.Cloud.csproj --configuration Release
```

Expected: auth/session/cookie/CSRF/rate-limit/readiness/redaction tests PASS.

```bash
git add apps/cloud tests/Puntiro.IntegrationTests/Cloud
git commit -m "feat: expose secure admin authentication"
```

### Task 8: Expose Token Management and Integration Bearer Authentication

**Files:**
- Create: `apps/cloud/Auth/IntegrationBearerAuthenticationHandler.cs`
- Create: `apps/cloud/Auth/IntegrationScopeRequirement.cs`
- Create: `apps/cloud/Auth/IntegrationScopeAuthorizationHandler.cs`
- Create: `apps/cloud/Endpoints/IntegrationTokenEndpoints.cs`
- Modify: `apps/cloud/Configuration/CloudServiceCollectionExtensions.cs`
- Modify: `apps/cloud/Program.cs`
- Create: `tests/Puntiro.IntegrationTests/Cloud/IntegrationTokenApiTests.cs`
- Create: `tests/Puntiro.IntegrationTests/Cloud/IntegrationBearerAuthenticationTests.cs`
- Create: `docs/runbooks/integration-token-rotation.md`

**Interfaces:**
- Consumes: owner session + five-minute TOTP step-up, `IIntegrationTokenService`, active organization checks.
- Produces routes and reusable policies:

```text
GET  /api/admin/integration-tokens
POST /api/admin/integration-tokens
POST /api/admin/integration-tokens/{id}/revoke
Policies: integration.shipments.read, integration.shipments.write
```

- [ ] **Step 1: Write HTTP and bearer RED tests**

```csharp
[Fact]
public async Task Create_returns_raw_token_once_and_list_never_returns_it()
{
    await LoginAndStepUpAsync();
    var created = await Client.PostAsJsonAsync("/api/admin/integration-tokens",
        new { displayName = "ERP", scopes = new[] { "shipments.write" } });
    var issued = await created.Content.ReadFromJsonAsync<IssuedIntegrationTokenResponse>();
    Assert.StartsWith("pnt_live_", issued!.Token);

    var list = await Client.GetStringAsync("/api/admin/integration-tokens");
    Assert.DoesNotContain(issued.Token, list);
    Assert.DoesNotContain("secretVerifier", list, StringComparison.OrdinalIgnoreCase);
}
```

Add tests for missing/stale step-up, CSRF, two-token limit, optimistic revoke, immediate bearer rejection after revoke, foreign tenant concealment, malformed Authorization, exact scope policy, bearer rate limiting and tenant context derived from token rather than request input.

Run the focused Cloud tests and expect missing-route FAIL.

- [ ] **Step 2: Implement Admin token management routes**

DTOs:

```csharp
public sealed record CreateIntegrationTokenRequest(string DisplayName, string[] Scopes);
public sealed class IssuedIntegrationTokenResponse
{
    public required Guid Id { get; init; }
    public required string PublicId { get; init; }
    public required string DisplayName { get; init; }
    public required string[] Scopes { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required string Token { get; init; }
    public override string ToString() => nameof(IssuedIntegrationTokenResponse);
}
public sealed record IntegrationTokenResponse(
    Guid Id, string PublicId, string DisplayName, string[] Scopes,
    DateTimeOffset CreatedAt, DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt, long Version);
public sealed record RevokeIntegrationTokenRequest(long ExpectedVersion, string Reason);
```

List returns `200`, create returns `201` with the one-time secret, and revoke returns `204`.

Every route requires authenticated active owner and antiforgery. Create additionally checks `SecondFactorVerifiedAt >= now - 5 minutes`. Map token limit to `409 integration_token.active_limit`, stale step-up to `403 auth.step_up_required`, concurrency to `409 integration_token.version_conflict`, and foreign/missing token to the same `404 integration_token.not_found`.

- [ ] **Step 3: Implement bearer scheme and scope policies**

The handler accepts exactly one `Authorization: Bearer` value, enforces a bounded token length, calls `AuthenticateAsync`, rechecks organization active state, and emits token/organization/scope claims. It never falls back to Admin cookie on `/api/v1` policy checks.

Apply a fixed-window limiter of 120 requests per minute per verified public token ID plus IP, with an IP-only partition for malformed/unknown credentials. A `429` response carries `Retry-After` and never confirms whether a public token ID exists.

Register `integration.shipments.read` and `integration.shipments.write` requirements. In the integration test host, map a test-only probe after `Program` composition to prove each policy without introducing a production auth-check endpoint.

Extend the OpenAPI transformers with a Bearer `IntegrationToken` scheme and the exact scope requirement on integration-protected test operations; Admin token-management operations remain cookie-authenticated and antiforgery-protected.

- [ ] **Step 4: Run GREEN, inspect OpenAPI and document manual rotation**

```bash
dotnet test tests/Puntiro.IntegrationTests/Puntiro.IntegrationTests.csproj --configuration Release --filter "FullyQualifiedName~IntegrationTokenApi|FullyQualifiedName~IntegrationBearer"
dotnet build apps/cloud/Puntiro.Cloud.csproj --configuration Release
```

Verify OpenAPI marks Admin routes with cookie/antiforgery expectations and future integration policies with Bearer. The runbook demonstrates create second token, update external system header, verify request, then revoke first token; it contains no usable token.

- [ ] **Step 5: Commit the HTTP integration boundary**

```bash
git add apps/cloud/Auth apps/cloud/Endpoints/IntegrationTokenEndpoints.cs apps/cloud/Configuration apps/cloud/Program.cs tests/Puntiro.IntegrationTests/Cloud docs/runbooks/integration-token-rotation.md
git commit -m "feat: expose integration authentication"
```

### Task 9: Wire Local PostgreSQL, CI, Documentation and Final Security Gates

**Files:**
- Create: `infra/compose/cloud-development.yml`
- Create: `infra/compose/.env.cloud.example`
- Create: `scripts/check-cloud-security.mjs`
- Create: `scripts/check-cloud-security.test.mjs`
- Create: `docs/adr/0003-global-identity-and-credentials.md`
- Create: `docs/runbooks/cloud-development.md`
- Create: `docs/reference/cloud-configuration.md`
- Create: `docs/reference/cloud-authentication-api.md`
- Create: `docs/engineering/cloud-identity-validation.md`
- Modify: `AGENTS.md`
- Modify: `.gitignore`
- Modify: `README.md`
- Modify: `docs/README.md`
- Modify: `docs/runbooks/development-bootstrap.md`
- Modify: `scripts/check-docs.mjs`
- Modify: `scripts/check-docs.test.mjs`
- Modify: `.github/workflows/foundation.yml`
- Modify: `package.json`

**Interfaces:**
- Consumes: all previous tasks and existing foundation gates.
- Produces: reproducible local/CI PostgreSQL workflow, repository-level security guardrails and an evidence record that distinguishes automated validation from unrun deployment/hardware gates.

- [ ] **Step 1: Write repository security-contract RED tests**

`scripts/check-cloud-security.test.mjs` must inject mutations and prove the checker rejects:

```js
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
```

Also reject committed `ConnectionStrings__Puntiro`, populated HMAC example values, missing cookie policy, floating PostgreSQL tags, missing module docs and missing new validation commands in `AGENTS.md`.

Run:

```bash
node --test scripts/check-cloud-security.test.mjs
```

Expected: FAIL because checker and operational files do not exist.

- [ ] **Step 2: Add exact local PostgreSQL 17.10 workflow**

`infra/compose/cloud-development.yml` uses `postgres:17.10-bookworm`, a named volume, healthcheck `pg_isready`, loopback-only port mapping and values supplied from an ignored local env file. `.env.cloud.example` contains names and generation commands, not populated secrets.

Add `infra/compose/.env.cloud` to `.gitignore`. The local file exports `PUNTIRO_TEST_POSTGRES`; every test command below assumes the developer loaded it without echoing its contents.

The runbook gives exact steps:

```bash
docker compose --env-file infra/compose/.env.cloud -f infra/compose/cloud-development.yml up -d
dotnet tool restore
dotnet ef database update --project src/Puntiro.Modules.Tenancy --startup-project apps/cloud --context TenancyDbContext
dotnet ef database update --project src/Puntiro.Modules.Identity --startup-project apps/cloud --context IdentityDbContext
dotnet ef database update --project src/Puntiro.Modules.Integrations --startup-project apps/cloud --context IntegrationsDbContext
dotnet run --project tools/Puntiro.Provisioning -- bootstrap-owner --organization-name Puntiro --organization-slug puntiro --email owner@example.test
```

Document rollback as application rollback plus reviewed expand/contract migration handling; never recommend automatic down-migration on production data.

- [ ] **Step 3: Add CI PostgreSQL and exact gates**

Add a `cloud-identity` Ubuntu job to `.github/workflows/foundation.yml` with the existing pinned checkout/setup actions and service `postgres:17.10-bookworm`. Configure only CI fixture credentials, wait for health, then run:

```bash
dotnet tool restore
dotnet restore Puntiro.slnx --locked-mode
dotnet build Puntiro.slnx --configuration Release --no-restore
dotnet test tests/Puntiro.UnitTests/Puntiro.UnitTests.csproj --configuration Release --no-build
dotnet test tests/Puntiro.IntegrationTests/Puntiro.IntegrationTests.csproj --configuration Release --no-build
node scripts/check-cloud-security.mjs
```

Set `PUNTIRO_TEST_POSTGRES` only in the job environment. Keep package audit in the existing foundation gate and do not print connection strings or generated credentials.

- [ ] **Step 4: Complete documentation and AGENTS guidance**

ADR 0002 records global users + memberships, separate module schemas, Argon2id/TOTP, server sessions, HMAC-only integration tokens and rejected ASP.NET Identity default credential storage/OAuth exchange alternatives.

`docs/reference/cloud-configuration.md` lists every variable, format and secret/non-secret classification. `docs/reference/cloud-authentication-api.md` lists routes, cookie/CSRF/Bearer behavior, scopes and stable problem codes. `docs/engineering/cloud-identity-validation.md` records exact automated evidence and leaves Timeweb deployment, restore drill and external-system acceptance as `not run`.

Extend `scripts/check-docs.mjs` and its tests so the documentation map, ADR, three module documents, four runbooks/references and validation record are required and linked.

Add these commands to `AGENTS.md` and development bootstrap:

```bash
dotnet tool restore
dotnet test tests/Puntiro.UnitTests/Puntiro.UnitTests.csproj --configuration Release
dotnet test tests/Puntiro.IntegrationTests/Puntiro.IntegrationTests.csproj --configuration Release
node scripts/check-cloud-security.mjs
```

Add `test:cloud:contracts` to `package.json`; include it in `check:foundation` only if it requires no PostgreSQL. Keep real PostgreSQL tests in the dedicated CI job.

- [ ] **Step 5: Run the complete fresh verification matrix**

With Node `24.19.0`, pnpm `11.17.0`, .NET SDK `10.0.302` and local PostgreSQL `17.10`:

```bash
corepack pnpm install --frozen-lockfile
corepack pnpm docs:check
corepack pnpm dependencies:check
corepack pnpm dependencies:audit
corepack pnpm test:repository
corepack pnpm foundation:check
corepack pnpm test:cloud:contracts
dotnet tool restore
dotnet restore Puntiro.slnx --locked-mode
dotnet build Puntiro.slnx --configuration Release --no-restore
dotnet test tests/Puntiro.UnitTests/Puntiro.UnitTests.csproj --configuration Release --no-build
dotnet test tests/Puntiro.IntegrationTests/Puntiro.IntegrationTests.csproj --configuration Release --no-build
git diff --check
```

Expected: every command exits `0`; no tests are skipped because PostgreSQL is absent; Cloud readiness is healthy only after migrations and keys; captured logs contain none of the fixture secrets.

- [ ] **Step 6: Inspect dependency/security outputs and commit**

Record exact package versions and audit result in `docs/engineering/cloud-identity-validation.md`. Confirm:

```bash
dotnet package list --project Puntiro.slnx --vulnerable --include-transitive
rg -n "MigrateAsync|EnsureCreated|RequestBody|ResponseBody" apps/cloud src tools
rg -n "pnt_live_|__Host-puntiro_session|recovery" docs apps src tools tests
git status --short
```

Review every match: production migration/body logging is absent; token strings appear only as format literals or test fixtures; no valid secret is committed.

```bash
git add AGENTS.md README.md package.json .github infra scripts docs
git commit -m "chore: validate cloud identity delivery"
```

## Self-Review Coverage Map

| Design section | Implemented by |
| --- | --- |
| Module boundaries and ownership | Tasks 1, 2, 4, 6, 7, 8 |
| Identity/Tenancy/Integrations data model | Tasks 2, 4, 6 |
| First-owner bootstrap and interrupted resume | Tasks 4, 5 |
| Owner TOTP recovery | Tasks 4, 5 |
| Argon2id, TOTP, recovery and email normalization | Tasks 3, 4 |
| Server sessions, cookie, CSRF and step-up | Tasks 4, 7 |
| Integration-token issuance, scope, rotation and revoke | Tasks 6, 8 |
| HTTP problems, rate limits and tenant derivation | Tasks 7, 8 |
| Key management and readiness | Tasks 4, 6, 7, 9 |
| EF migrations and real PostgreSQL | Tasks 2, 4, 6, 7, 9 |
| Redacted security events and observability | Tasks 2, 4, 6, 7, 9 |
| Dependencies, CI, docs and repository guidance | Tasks 1, 9 |

The plan intentionally creates no Shipment, Template, Fleet, Agent, printer, Admin UI, OAuth or Timeweb deployment behavior. Those remain in later roadmap stages.

## Final Review Checkpoint

After Task 9, request a fresh spec-compliance and code-quality review over the full range from `e39b626` to HEAD. Reviewers must verify at minimum:

- no module-to-module project or table access;
- global email uniqueness and membership-only organization access;
- provisioning resume cannot hijack an existing account;
- TOTP and recovery use are atomic and replay-safe;
- recovery reset revokes every old session and credential batch;
- session idle/absolute boundaries and step-up semantics;
- raw token/session secrets never persist or log;
- integration token scope, tenant derivation, two-active limit and immediate revoke;
- CSRF, secure cookie, generic auth errors and rate limits;
- real PostgreSQL migrations and no runtime migration;
- exact dependency pins, clean audits, documentation and CI evidence.

Any finding starts a new RED test and a separate fix commit. Do not amend reviewed task commits. Re-run the complete Task 9 matrix after the last fix.
