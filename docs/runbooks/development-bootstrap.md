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
dotnet build Puntiro.slnx --configuration Release
corepack pnpm check:foundation
corepack pnpm check
```

`dependencies:check` enforces exact stable dependency and toolchain pins. `dependencies:audit` checks all npm dependencies at high severity and transitive NuGet packages for known vulnerabilities using the configured official sources. `docs:check` validates the repository documentation contract. `check:foundation` is the aggregate repository-foundation gate. `check` runs the existing design-system validation pipeline; these commands are automated evidence only and do not replace Windows or physical-hardware acceptance.

The repository-level `.npmrc` fixes dependency resolution to the public npm registry. Keep `pnpm-lock.yaml` free of machine-specific registry URLs so the same frozen lockfile is verifiable locally and on GitHub Actions.
