# Cloud Identity and Tenancy Validation

Date: 2026-08-09

This record separates automated evidence from deployment and physical acceptance. It contains no credentials, tokens, connection strings, label payloads, or personal data.

## Pinned runtime and dependencies

- Node.js: `24.19.0` required by `package.json` and CI.
- pnpm: `11.17.0` through Corepack.
- .NET SDK: `10.0.302` with roll-forward disabled.
- PostgreSQL: `postgres:17.10-bookworm` locally and in CI.
- EF tool: `dotnet-ef` `10.0.10`.
- EF Core: `10.0.10`; Npgsql EF provider: `10.0.3`.
- ASP.NET Core OpenAPI/MVC testing: `10.0.10`.
- Argon2: `Konscious.Security.Cryptography.Argon2` `1.3.1`.
- Test SDK/xUnit/runner: `18.8.1`, `3.2.2`, `3.1.5`.

Exact versions are pinned in repository manifests and lockfiles. The dependency audit command covers all npm dependencies at high severity plus transitive NuGet packages.

## Automated evidence

The following commands passed locally in this checkout with the pinned runtimes above:

- `corepack pnpm install --frozen-lockfile`;
- `corepack pnpm dependencies:check` and `corepack pnpm dependencies:audit` — no known npm or transitive NuGet vulnerabilities reported by the configured registries;
- `corepack pnpm docs:check`;
- `corepack pnpm test:repository` — 74 passed, 0 failed;
- `corepack pnpm test:cloud:contracts` — 60 passed, 0 failed, including Git-enumerated secret/config probes, strict secret-safe dotenv execution, normalized-Compose portability, normal runtime-material preflight and isolated restore validation;
- `corepack pnpm test:cloud:compose` — 7 passed, 0 failed, covering database-only configuration, mandatory Cloud runtime files, disabled/proxy-only/network-only/combined proxy values, arbitrary retained HMAC versions, and non-creating Data Protection/certificate binds across Compose implementations that retain or omit explicit `false` in normalized JSON;
- `corepack pnpm check:foundation` — aggregate documentation, dependency, repository, boundary, UI package and Cloud security checks passed;
- `corepack pnpm check` — UI unit tests 78 passed, Storybook tests 171 passed, the static Storybook build passed, and Playwright tests 28 passed;
- `dotnet tool restore` and `dotnet restore Puntiro.slnx --locked-mode`;
- `dotnet build Puntiro.slnx --configuration Release --no-restore` — 0 warnings and 0 errors;
- `dotnet test tests/Puntiro.UnitTests/Puntiro.UnitTests.csproj --configuration Release --no-build` — 179 passed, 0 failed, 0 skipped;
- `dotnet test tests/Puntiro.IntegrationTests/Puntiro.IntegrationTests.csproj --configuration Release --no-build` against a disposable loopback-only `postgres:17.10-bookworm` instance — 145 passed, 0 failed, 0 skipped;
- the trusted-proxy startup/behavior subset — 10 passed, covering exact and narrow trusted boundaries, an untrusted peer with spoofed `X-Forwarded-For`, empty trust failure, unrestricted IPv4/IPv6 boundary rejection and the forbidden platform shortcut; exact environment binder cases added another 5 passing cases;
- database-only and opt-in `cloud-runtime` Compose configurations passed executable `docker compose config` tests with disposable validation-only values supplied by local files; the Cloud profile fails closed without its ignored runtime environment, existing key ring and certificate, and no values were committed;
- the normal-startup preflight verified private runtime-env, Data Protection key-ring and certificate modes, ownership and protected key material; the restore validator rejected normal-database targets and required matching isolated container/host-tool targets plus restored protected cryptographic material before container creation;
- repository-wide Git-enumerated text/configuration scans found no runtime migration/service resolution, request/response-body logging, populated repository runtime credentials, or usable raw/base64-encoded integration-token secret. Ignored untracked local files are exempt; force-tracked ignored and non-ignored untracked configuration are scanned.

The immutable GitHub Actions workflow and its pinned runtime/service contract passed local mutation tests. The first hosted PR run exposed a Windows temporary-path canonicalization false positive and an order-dependent shared-schema assertion. Both failures were reproduced with RED tests and fixed locally; the hosted rerun is pending.

## Operational and manual gates

- Timeweb production deployment: **not run**.
- Timeweb trusted reverse-proxy acceptance: **not run**.
- Production PostgreSQL migration/rollback: **not run**.
- PostgreSQL plus Data Protection/certificate/HMAC restore drill: **not run**.
- HMAC/certificate custody and production rotation drill: **not run**.
- External system configured request and manual token rotation: **not run**.
- Windows Agent, WPF kiosk, real touch/gloves, screen reader and physical Zebra/TSC/Bixolon printers: **not run** and outside this backend stage.

Automated browser, HTTP and PostgreSQL evidence does not upgrade these gates.
