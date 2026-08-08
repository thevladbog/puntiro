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
- Documentation: `corepack pnpm docs:check`
- Existing UI pipeline: `corepack pnpm check`

## Dependency Policy

- Resolve current official documentation through Context7 before adding or updating a library.
- Check official security advisories after Context7; Context7 does not replace vulnerability review.
- Pin exact stable versions. Do not introduce ranges, prereleases, or floating SDKs.
- Update lockfiles, SBOM inputs, documentation, and tests in the same change.

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
