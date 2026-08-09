# Puntiro Repository Instructions

Read this file before changing the repository. The closest nested `AGENTS.md` may add stricter local rules but cannot weaken this file.

## Architecture Invariants

- Cloud owns shipment intent, configuration, and durable audit.
- Windows Agent owns SQLite, offline grace, label sets, and physical print attempts.
- React UI never opens SQLite, holds device credentials, or talks to printers.
- Printing is forbidden before a durable local transaction stores exact payload bytes.
- `unknown` print results are never retried or archived automatically.

## Required Commands

- Bootstrap: `corepack pnpm install --frozen-lockfile`
- Dependencies: `corepack pnpm dependencies:check`
- Vulnerability audit: `corepack pnpm dependencies:audit`
- Documentation: `corepack pnpm docs:check`
- Foundation boundaries: `corepack pnpm foundation:check`
- Foundation aggregate: `corepack pnpm check:foundation`
- .NET solution: `dotnet build Puntiro.slnx --configuration Release`
- .NET tools: `dotnet tool restore`
- Cloud unit tests: `dotnet test tests/Puntiro.UnitTests/Puntiro.UnitTests.csproj --configuration Release`
- Cloud PostgreSQL tests: `dotnet test tests/Puntiro.IntegrationTests/Puntiro.IntegrationTests.csproj --configuration Release`
- Cloud security contracts: `node scripts/check-cloud-security.mjs`
- Cloud security mutation suite: `corepack pnpm test:cloud:contracts`
- Executable Compose contracts: `corepack pnpm test:cloud:compose`
- Cloud runtime preflight: `node scripts/preflight-cloud-runtime.mjs --compose-env infra/compose/.env.cloud --runtime-env infra/compose/cloud-runtime.env`
- Existing UI pipeline: `corepack pnpm check`

## Cloud Deployment Safety

- Production Cloud never calls `Migrate`, `MigrateAsync`, `EnsureCreated`, or `EnsureCreatedAsync`; apply reviewed module migrations explicitly before starting the host.
- Pin local and CI PostgreSQL to `postgres:17.10-bookworm`. Persist database data, the Data Protection key ring, its protection certificate custody, and every retained HMAC version as distinct recovery concerns.
- Forwarded headers are disabled unless the exact immediate proxy IP/network allowlist is configured. Keep one symmetric hop, never enable `ASPNETCORE_FORWARDEDHEADERS_ENABLED`, and test Timeweb after every topology change.
- `/health/live` proves process liveness only. Traffic is allowed only after `/health/ready` confirms PostgreSQL, migrations, Data Protection, and current/historical HMAC requirements.
- Roll back application code with expand/contract-compatible schema and retained keys. Never automate a destructive EF down-migration against production data.
- Keep populated `.env.cloud` and `cloud-runtime.env` files both ignored and untracked at mode `0600`. They are raw dotenv data for Compose and `scripts/run-with-cloud-env.mjs`; never source them as shell code.
- Runtime dotenv files reject duplicate assignments, quotes, inline comments, shell substitutions, backticks, control characters and leading/trailing value whitespace. A later file may deliberately override a key from an earlier file for host tooling.
- Pass proxy indices and every retained HMAC `Keys__<version>` through the ignored raw runtime env file. Do not enumerate versions or create blank indexed proxy values in Compose.
- Host provisioning and Cloud must use the same persistent host Data Protection ring through the documented bind mount. Before normal Cloud creation/start, require the runtime env, exact service UID/GID ownership, a mode-`0700` nonempty valid ring and mode-`0600` certificate through `scripts/preflight-cloud-runtime.mjs`; bind mounts use `create_host_path: false`.
- Restore validation parses the container and both host-tool connections, requires the same isolated database/credentials through a distinct loopback port, and validates restored DP/certificate/HMAC material before any restore-project service is created or started.

## Dependency Policy

- Resolve current official documentation through Context7 before adding or updating a library.
- Check official security advisories after Context7; Context7 does not replace vulnerability review.
- Pin exact stable registry npm, NuGet, and tool versions. Only first-party `@puntiro/*` workspace packages may use `workspace:*`; do not introduce other ranges, prereleases, or floating SDKs.
- Keep a valid sibling `packages.lock.json` for every project listed in `Puntiro.slnx`, including project-only/BCL-only projects, so locked restore never creates unreviewed files.
- Update lockfiles, SBOM inputs, documentation, and tests in the same change.

## Internal Access Policy

The protected production assembly identities are `Puntiro.Security`, `Puntiro.Modules.Identity`, `Puntiro.Modules.Tenancy`, `Puntiro.Modules.Integrations`, and `Puntiro.Provisioning`. Each built assembly must contain exactly these two friend names in genuine BCL `InternalsVisibleToAttribute` metadata, once each, and no other friend metadata:

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Puntiro.UnitTests")]
[assembly: InternalsVisibleTo("Puntiro.IntegrationTests")]
```

`<project>/Properties/AssemblyInfo.cs` with the block above is the preferred repository convention because it is easy to review. It is not a security invariant or a required metadata origin. Equivalent declarations may be relocated or produced by MSBuild, and the `InternalsVisibleTo` token is not reserved in other source, comments, strings, or build files. Only the effective metadata in the protected assembly is authoritative.

The fast `foundation:check` enforces reviewable repository-file boundaries, not friend-metadata origin or the complete imported MSBuild graph. The mandatory `check:dotnet` requires the normalized `Puntiro.slnx` project set to equal the approved graph exactly. For every managed project it runs `ResolveProjectReferences` without building dependencies, checks both the effective direct references and the references/results actually consumed by MSBuild, and requires the exact approved acyclic graph across arbitrary and inherited imports. It then performs locked restores and builds the complete solution in an empty per-run artifacts root, followed by the BCL-only verifier and every protected project separately under unique `--artifacts-path` roots.

Before and after each protected build, the command binds the requested project path and expected assembly name to MSBuild's evaluated `TargetPath`, `OutputPath`, `IntermediateOutputPath`, and `ArtifactsPath`. Outputs and intermediates must remain inside that project's unique root. Every isolated artifacts tree is walked with `lstat` and checked with `realpath`; symlinks, junctions/reparse points, escaped paths, and non-file targets are rejected. The verifier reads only those exact regular `TargetPath` files, asserts their assembly identities, and compares the complete friend-name multiset with the two-name allowlist.

Normal `bin` and `obj` files, filename searches in a shared tree, timestamps, and execution of protected assemblies are never accepted as evidence. Friend metadata counts only when its member reference is the exact `.ctor(string)` on the trusted .NET 10 BCL assembly identity and public key token; a same-named or forged type/member/reference is ignored. A solution/graph mismatch or cycle, missing target, changed or escaped build plan, linked artifact, duplicate expected identity or path, assembly-identity mismatch, or missing, duplicate, or unapproved friend name fails closed with diagnostics pointing to this section.

This policy validates the final regular file at the successful build invocation's exact project-scoped `TargetPath`; it does not prove which compiler inputs produced those bytes or defend against malicious/tampered build definitions. A build target may replace that regular file, including by copying another assembly, and it is accepted when the final assembly identity and exact friend allowlist remain valid. Compiler provenance and build-supply-chain integrity require signed/reproducible-build controls in a separate future stage.

## Documentation Policy

- Update useful documentation in the same change as behavior.
- Do not describe planned behavior as implemented.
- Add an ADR for decisions that are expensive to reverse.
- Keep OpenAPI, IPC reference, runbooks, and commands executable and current.

## Validation Boundaries

- Browser and CI checks are automated evidence only.
- Windows, real touch, gloves, screen readers, and physical printers remain `not run` until performed on that environment.
- Kiosk compositions must have no page scroll and no touch target below the approved minimum.

## Secrets and Logs

- Never commit or print passwords, integration tokens, device credentials, activation codes, raw label payloads, or personal data.
- Logs must be structured, bounded, and redacted.

## Git Safety

- Preserve unrelated user changes and untracked local data.
- Do not use destructive reset or checkout commands without explicit approval.
- Stage only files owned by the current task and use focused commits.
