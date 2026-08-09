# Development Bootstrap

## Prerequisites

- Node.js 24.19.0
- Corepack with pnpm 11.17.0, as pinned by the repository
- .NET SDK 10.0.302
- Docker with Compose for the pinned local PostgreSQL 17.10 service
- Local Chromium installed by Playwright for browser validation

## Commands

Run these commands from the repository root:

```bash
node --version
corepack pnpm --version
dotnet --info
corepack pnpm install --frozen-lockfile
corepack pnpm dependencies:check
corepack pnpm dependencies:audit
corepack pnpm docs:check
corepack pnpm foundation:check
corepack pnpm check:dotnet
corepack pnpm check:foundation
corepack pnpm test:cloud:contracts
corepack pnpm test:cloud:compose
dotnet tool restore
dotnet test tests/Puntiro.UnitTests/Puntiro.UnitTests.csproj --configuration Release
dotnet test tests/Puntiro.IntegrationTests/Puntiro.IntegrationTests.csproj --configuration Release
node scripts/check-cloud-security.mjs
corepack pnpm check
```

`dependencies:check` enforces exact stable dependency and toolchain pins. `dependencies:audit` checks all npm dependencies at high severity and transitive NuGet packages for known vulnerabilities using the configured official sources. `docs:check` validates the repository documentation contract. `foundation:check` performs the fast source and graph checks.

`check:dotnet` is the definitive .NET policy chain. Every project in `Puntiro.slnx` has a reviewed sibling `packages.lock.json`, including projects whose lock contains only project references or an empty target-framework graph, so locked restores leave the checkout unchanged. The command first requires the normalized `Puntiro.slnx` project set to equal the approved graph exactly. Before the definitive locked restore/build, it creates disposable per-project restore assets and runs `ResolveProjectReferences` with dependency builds disabled. It compares each project's effective direct `ProjectReference` items, final build-consumed reference items, and resolved project producers with the approved direct graph and transitive closure; explicit, arbitrary, inherited, pre-target, and late consumed-item mutations are therefore visible. The command then restores and builds the complete solution beneath a unique empty OS-temporary artifacts root. It restores and builds the BCL metadata verifier and each protected project individually, each with a distinct `--artifacts-path` that isolates both `bin` outputs and `obj` intermediates.

For every individual build, the command evaluates `MSBuildProjectFullPath`, `AssemblyName`, `TargetPath`, `OutputPath`, `IntermediateOutputPath`, and `ArtifactsPath` before and after compilation. The evaluated project and identity must match the requested protected producer, all paths must stay within its unique artifacts root, the plan must remain stable, and the exact `TargetPath` must be a regular file. The OS-owned `mkdtemp` result is canonicalized before artifact paths are derived, avoiding false rejection of Windows short-name aliases. The complete derived tree is then checked with platform-neutral `lstat`/`realpath`; symbolic links and Windows junction/reparse redirections inside that tree fail closed. The verifier receives explicit expected-identity/path pairs and reads managed metadata without executing the protected assemblies. Each protected identity must expose exactly `Puntiro.UnitTests` and `Puntiro.IntegrationTests`, once each, through the trusted .NET 10 BCL assembly identity and exact `.ctor(string)` member signature, with no other friends; a local or forged same-named reference is not accepted.

The declaration's source location is deliberately not part of this policy. `Properties/AssemblyInfo.cs` is the preferred review convention, but equivalent relocated or MSBuild-generated declarations are accepted when the final metadata is exact. Normal `bin` or `obj` files and same-named DLLs from another project are never searched or read. The successful build's exact project-scoped regular-file `TargetPath` is authoritative even when an MSBuild target explicitly replaces it with another regular file whose final assembly identity and allowlist are exact. This gate does not establish compiler/artifact provenance or build-supply-chain integrity; those require signed/reproducible-build controls in a future stage. Missing or duplicate inputs, identity mismatches, escaped paths, stale-only outputs, and missing, duplicate, or unapproved friends fail with diagnostics referencing `AGENTS.md#internal-access-policy`; all temporary artifacts are removed on success or failure.

`check:foundation` is the aggregate repository-foundation gate and includes both the fast and definitive layers. `check` runs the existing design-system validation pipeline; these commands are automated evidence only and do not replace Windows or physical-hardware acceptance.

`test:cloud:contracts` runs secret-free repository mutation tests and requires no PostgreSQL. `test:cloud:compose` resolves the production-shaped Compose contract and requires Docker Compose, but starts no container. Starting Cloud is a separate operator action: the [Cloud operations runbook](cloud-development.md) requires the checked wrapper, which fails until the ignored runtime env, nonempty protected Data Protection ring, certificate/password and service UID/GID ownership have been provisioned. The integration-test command requires a secret `PUNTIRO_TEST_POSTGRES` maintenance connection to real PostgreSQL 17.10 and fails rather than skipping when it is absent. Production Cloud never applies migrations at startup.

The repository-level `.npmrc` fixes dependency resolution to the public npm registry. Keep `pnpm-lock.yaml` free of machine-specific registry URLs so the same frozen lockfile is verifiable locally and on GitHub Actions.
