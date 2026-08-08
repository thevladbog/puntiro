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

The protected production assembly identities are `Puntiro.Security`, `Puntiro.Modules.Identity`, `Puntiro.Modules.Tenancy`, `Puntiro.Modules.Integrations`, and `Puntiro.Provisioning`. Each built assembly must contain exactly these two friend names in genuine BCL `InternalsVisibleToAttribute` metadata, once each, and no other friend metadata:

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Puntiro.UnitTests")]
[assembly: InternalsVisibleTo("Puntiro.IntegrationTests")]
```

`<project>/Properties/AssemblyInfo.cs` with the block above is the preferred repository convention because it is easy to review. It is not a security invariant or a required metadata origin. Equivalent declarations may be relocated or produced by MSBuild, and the `InternalsVisibleTo` token is not reserved in other source, comments, strings, or build files. Only the effective metadata in the protected assembly is authoritative.

The fast `foundation:check` enforces repository graph and boundary rules, not friend-metadata origin. The mandatory `check:dotnet` performs locked restores and builds the complete solution in an empty per-run artifacts root. It then restores, evaluates, and builds the BCL-only verifier and every protected project separately, each under its own unique `--artifacts-path`. Before and after each build it binds the requested project path and expected assembly name to MSBuild's evaluated `TargetPath`, `OutputPath`, `IntermediateOutputPath`, and `ArtifactsPath`; outputs and intermediates must remain inside that project's unique root. The verifier reads only those exact `TargetPath` files, asserts their assembly identities, and compares the complete friend-name multiset with the two-name allowlist.

Normal `bin` and `obj` files, filename searches in a shared tree, timestamps, and execution of protected assemblies are never accepted as evidence. A same-named attribute defined outside the BCL is not friend metadata. A missing target, changed or escaped build plan, duplicate expected identity or path, assembly-identity mismatch, or missing, duplicate, or unapproved friend name fails closed with diagnostics pointing to this section.

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
