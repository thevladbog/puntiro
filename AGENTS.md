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
- Existing UI pipeline: `corepack pnpm check`

## Dependency Policy

- Resolve current official documentation through Context7 before adding or updating a library.
- Check official security advisories after Context7; Context7 does not replace vulnerability review.
- Pin exact stable registry npm, NuGet, and tool versions. Only first-party `@puntiro/*` workspace packages may use `workspace:*`; do not introduce other ranges, prereleases, or floating SDKs.
- Keep a valid sibling `packages.lock.json` for every project listed in `Puntiro.slnx`, including project-only/BCL-only projects, so locked restore never creates unreviewed files.
- Update lockfiles, SBOM inputs, documentation, and tests in the same change.

## Internal Access Policy

Production projects that expose internals to tests must keep the only two friend declarations in `<project>/Properties/AssemblyInfo.cs`, using this canonical block:

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Puntiro.UnitTests")]
[assembly: InternalsVisibleTo("Puntiro.IntegrationTests")]
```

CRLF line endings are normalized and accepted; every other character, declaration order, blank line, and formatting detail must match the block. The `InternalsVisibleTo` token is reserved outside that canonical file, including in comments and ordinary strings. Do not configure friend access through `.csproj`, imported or inherited MSBuild `.props`/`.targets`, aliases, generated sources, escapes, or entities.

The fast `foundation:check` enforces the reviewable source convention. The mandatory `check:dotnet` then restores in locked mode, asks MSBuild to evaluate the Release `Compile`, `AssemblyAttribute`, legacy `AssemblyAttributes`, and `InternalsVisibleTo` inputs (including arbitrary imports), requires the canonical file to be compiled exactly once, and rejects alternative generated friend attributes. It builds the solution into a unique empty OS-temporary `BaseOutputPath`, requires exactly one output for every protected assembly and the BCL-only verifier, and verifies only those exact outputs with metadata inspection. Fixed `bin/Release` files, a successful build exit code, timestamps, and assembly execution are never accepted as provenance or freshness proof. Diagnostics point back to this section.

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
