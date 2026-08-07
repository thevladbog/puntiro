# Puntiro Design System Platform

Puntiro Design System Platform is the local source of truth for Puntiro product UI: design tokens, accessible React components, kiosk patterns, composition rules, bilingual documentation, and executable Storybook examples.

This repository contains no product API, persistence, routing, printer I/O, or completed application screens. Compositions remain a documented contract until a separate design and approval stage.

## Prerequisites

- Node 24
- pnpm 11.17.0
- Local Chromium installed by Playwright

## Getting started

```bash
corepack enable
pnpm install --frozen-lockfile
pnpm storybook
```

The exact package-manager version is pinned in `packageManager`. In automation or environments where changing the global Corepack shims is undesirable, prefix commands with `corepack`, for example `corepack pnpm check`.

## Validation commands

```bash
pnpm tokens:check
pnpm typecheck
pnpm lint
pnpm test
pnpm test:storybook
pnpm storybook:build
pnpm test:visual
pnpm check
```

`pnpm check` always runs the same local pipeline in this order:

1. deterministic token generation check;
2. TypeScript typecheck;
3. ESLint;
4. unit tests;
5. Storybook browser interaction and accessibility tests in Chromium;
6. static Storybook build;
7. local Playwright visual and kiosk-contract tests.

The package scripts invoke pinned pnpm through Corepack internally, so `corepack pnpm check` does not depend on a machine-wide pnpm binary. Visual baselines and fonts are local; no paid cloud service is used.

## Maturity and validation report

The first-wave manifest lives in `apps/storybook/src/docs/maturity-manifest.ts`. All 19 exports start at **Beta**. A passing automated pipeline is evidence for automated checks only; it never marks a manual gate as approved.

Record releases using separate automated and manual sections:

```text
Automated validation
- tokens:check: pass / fail / not run
- typecheck: pass / fail / not run
- lint: pass / fail / not run
- unit: pass / fail / not run; <count>
- Storybook browser: pass / fail / not run; <count>
- static build: pass / fail / not run
- local visual and kiosk contracts: pass / fail / not run; <count>

Manual validation
- Windows kiosk at 1280 × 800: pass / fail / not run
- real touch display: pass / fail / not run
- gloves: pass / fail / not run
- motion review: pass / fail / not run
- screen reader: pass / fail / not run
- physical Zebra, TSC, and Bixolon printers: pass / fail / not run
```

Manual gates remain `not run` until a reviewer performs them on the stated hardware. They are not inferred from browser emulation, axe, screenshots, or any other automated result.
