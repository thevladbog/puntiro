# Development Bootstrap

## Prerequisites

- Node.js 24.19.0
- Corepack with pnpm 11.17.0, as pinned by the repository
- .NET SDK 10.0.302
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
corepack pnpm check
```

`dependencies:check` enforces exact stable dependency and toolchain pins. `dependencies:audit` checks all npm dependencies at high severity and transitive NuGet packages for known vulnerabilities using the configured official sources. `docs:check` validates the repository documentation contract. `foundation:check` performs the fast source and graph checks.

`check:dotnet` is the definitive .NET policy chain. Every project in `Puntiro.slnx` has a reviewed sibling `packages.lock.json`, including projects whose lock contains only project references or an empty target-framework graph, so locked restores leave the checkout unchanged. The command first restores and builds the complete solution beneath a unique empty OS-temporary artifacts root. It then restores and builds the BCL metadata verifier and each protected project individually, each with a distinct `--artifacts-path` that isolates both `bin` outputs and `obj` intermediates.

For every individual build, the command evaluates `MSBuildProjectFullPath`, `AssemblyName`, `TargetPath`, `OutputPath`, `IntermediateOutputPath`, and `ArtifactsPath` before and after compilation. The evaluated project and identity must match the requested protected producer, all paths must stay within its unique artifacts root, the plan must remain stable, and the exact `TargetPath` must exist. The verifier receives explicit expected-identity/path pairs and reads managed metadata without executing the protected assemblies. Each protected identity must expose exactly `Puntiro.UnitTests` and `Puntiro.IntegrationTests`, once each, through the BCL attribute type, with no other friends; a local same-named attribute is not accepted.

The declaration's source location is deliberately not part of this policy. `Properties/AssemblyInfo.cs` is the preferred review convention, but equivalent relocated or MSBuild-generated declarations are accepted when the final metadata is exact. Normal `bin` or `obj` files and same-named DLLs from another project are never searched or read. Missing or duplicate inputs, identity mismatches, escaped paths, stale-only outputs, and missing, duplicate, or unapproved friends fail with diagnostics referencing `AGENTS.md#internal-access-policy`; all temporary artifacts are removed on success or failure.

`check:foundation` is the aggregate repository-foundation gate and includes both the fast and definitive layers. `check` runs the existing design-system validation pipeline; these commands are automated evidence only and do not replace Windows or physical-hardware acceptance.

The repository-level `.npmrc` fixes dependency resolution to the public npm registry. Keep `pnpm-lock.yaml` free of machine-specific registry URLs so the same frozen lockfile is verifiable locally and on GitHub Actions.
