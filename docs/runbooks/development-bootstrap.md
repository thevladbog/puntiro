# Development Bootstrap

## Prerequisites

- Node.js 24.19.0
- Corepack with pnpm 11.17.0, as pinned by the repository
- .NET SDK available for the planned cloud and Windows components
- Local Chromium installed by Playwright for browser validation

## Commands

Run these commands from the repository root:

```bash
node --version
corepack pnpm --version
dotnet --info
corepack pnpm install --frozen-lockfile
corepack pnpm dependencies:check
corepack pnpm docs:check
corepack pnpm check
```

`dependencies:check` enforces exact stable dependency and toolchain pins. `docs:check` validates the repository documentation contract. `check` runs the existing design-system validation pipeline; it is automated evidence only and does not replace Windows or physical-hardware acceptance.
