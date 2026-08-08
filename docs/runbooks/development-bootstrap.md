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

`check:dotnet` is the definitive .NET policy chain. Every project in `Puntiro.slnx` has a reviewed sibling `packages.lock.json`, including projects whose lock contains only project references or an empty target-framework graph, so the initial locked restore leaves the checkout unchanged. The command then evaluates the Release MSBuild `Compile`, `AssemblyAttribute`, legacy `AssemblyAttributes`, and `InternalsVisibleTo` items for every protected project and requires `Properties/AssemblyInfo.cs` to remain an effective compiler input exactly once. It rejects MSBuild-generated friend access, including values decoded from arbitrary imports. The command builds into a unique empty temporary output root, requires exactly one expected DLL per protected assembly, and passes only those outputs to the BCL metadata verifier. A stale DLL under a project's normal `bin/Release` directory is never read. Missing, duplicate, relocated, or unapproved inputs and outputs fail with diagnostics referencing `AGENTS.md#internal-access-policy`; the temporary output is removed on success or failure.

`check:foundation` is the aggregate repository-foundation gate and includes both the fast and definitive layers. `check` runs the existing design-system validation pipeline; these commands are automated evidence only and do not replace Windows or physical-hardware acceptance.

The repository-level `.npmrc` fixes dependency resolution to the public npm registry. Keep `pnpm-lock.yaml` free of machine-specific registry URLs so the same frozen lockfile is verifiable locally and on GitHub Actions.
