# Puntiro Design System Platform Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a bilingual, branded Storybook platform and production-ready Puntiro component foundation for touch-first kiosk patterns and standard-density admin UI.

**Architecture:** Use a pnpm workspace with a DTCG token package, a framework-independent Puntiro React UI package, and a Storybook application that owns documentation, stories, interaction checks, and local visual baselines. React Aria Components supplies internal accessible behavior; Puntiro tokens, CSS Modules, public types, and providers define the visual and product contract.

**Tech Stack:** Node 24, pnpm 11, React 19, TypeScript 7, Vite 8, Storybook 10, React Aria Components, Style Dictionary 5, Vitest browser mode, Playwright Chromium, axe, CSS Modules, Lucide.

## Global Constraints

- Baseline kiosk viewport: 1280 × 800 landscape; layouts adapt upward.
- Kiosk pages and working regions must not scroll at the baseline viewport.
- Touch mode interactive targets are at least 64 px; 72 px is the comfortable primary-action size.
- Standard mode interactive targets are at least 44 px.
- Russian is the default locale; every Stable component and controlled documentation page also has English copy.
- Storybook manager chrome stays English; only Puntiro-owned docs and canvas content switch with `RU / EN`.
- Approved fonts are Onest and IBM Plex Mono; Outfit selected by the gpt-taste RNG is rejected by the approved brandbook.
- Approved motion durations are 120 ms and 180 ms; decorative GSAP and infinite motion are excluded from operational UI.
- Handoff Orange `#FF5A1F` communicates current action, selection, or transfer and is not large decorative fill.
- Register Ink `#171914`, Label Paper `#F2F0E8`, Label Paper Strong `#FFFDF6`, and Warehouse Steel `#8B9189` retain their approved values.
- Do not add a dark product theme, application routing, API access, persistence, printer I/O, ZPL/TSPL rendering, or full product screens.
- Do not add Chromatic or another paid visual-testing service.
- Public `@puntiro/ui` types must not expose React Aria or Lucide types.
- Exact dependency versions are pinned; package manifests do not use `^` or `~` ranges.
- Every implementation task follows red → green → refactor, runs its focused checks, and commits only its own files.

## Adapted gpt-taste preflight

<design_plan>
Python RNG execution:
seed=111; hero=Cinematic Center; font=Outfit
components=Feedback/Testimonial Carousel, Inline Typography Images, Horizontal Accordions
gsap=Card Stacking, Scrubbing Text Reveals

Applicability decision: Cinematic Center is used only as the restrained Start-page introduction. Outfit, testimonial carousel, inline images, hover accordion, card stacking, and scrubbing reveals conflict with the approved Puntiro brandbook, accessibility rules, and 120–180 ms operational motion, so they are explicitly excluded.

AIDA check for the Storybook Start page: Storybook manager provides Navigation; the centered two-line introduction provides Attention; a complete 2 × 2 system map provides Interest; a live component preview provides Desire; two high-contrast links to Foundations and Components provide Action. This is information architecture, not marketing copy.

Hero math: the intro heading uses `max-width: 1120px` and `font-size: clamp(48px, 5vw, 72px)`, which keeps the approved RU and EN headings within two or three lines at 1280 px. There are no stamp icons or pill-tag clusters.

Bento density: the Start-page system map has two columns × two rows = four cells, occupied by exactly four navigation modules. CSS uses `grid-auto-flow: dense`; there are zero unused cells.

Label and button check: no labels such as `SECTION 01` or `QUESTION 05` are used. Primary buttons use Handoff Orange with Register Ink text; secondary buttons use Label Paper Strong with Register Ink text. Contrast is verified with axe and manual review before Stable status.
</design_plan>

## Exact dependency baseline

The versions below were read from the npm registry on 2026-08-07 and must be recorded exactly in the first lockfile:

| Package | Version |
| --- | --- |
| `pnpm` | `11.17.0` |
| `react`, `react-dom` | `19.2.8` |
| `@types/react` | `19.2.17` |
| `@types/react-dom` | `19.2.3` |
| `typescript` | `7.0.2` |
| `vite` | `8.1.5` |
| `@vitejs/plugin-react` | `6.0.4` |
| `storybook`, `@storybook/react-vite`, `@storybook/addon-docs`, `@storybook/addon-a11y`, `@storybook/addon-vitest` | `10.5.3` |
| `vitest`, `@vitest/browser-playwright` | `4.1.10` |
| `playwright`, `@playwright/test` | `1.61.1` |
| `react-aria-components` | `1.19.0` |
| `style-dictionary` | `5.5.0` |
| `lucide-react` | `1.26.0` |
| `vite-plugin-dts` | `5.0.3` |
| `@fontsource/onest`, `@fontsource/ibm-plex-mono` | `5.3.0` |
| `eslint` | `10.7.0` |
| `typescript-eslint` | `8.65.0` |

## Planned file map

```text
/
  package.json                         root commands and exact tool versions
  pnpm-workspace.yaml                  workspace membership and pnpm policy
  pnpm-lock.yaml                       reproducible dependency graph
  tsconfig.base.json                   strict shared compiler settings
  eslint.config.js                     shared TypeScript and React rules
  vitest.config.ts                     Node/unit projects
  .gitignore                           generated and local artifacts
  README.md                            local design-system commands
  scripts/check-generated.mjs          verifies token outputs are committed

packages/tokens/
  package.json
  sd.config.mjs                        DTCG → CSS/JSON/TypeScript pipeline
  src/reference/*.tokens.json          approved brand values and scales
  src/semantic/*.tokens.json           role-based light product tokens
  src/component/*.tokens.json          narrowly scoped component aliases
  src/token-contract.test.ts            source and generated contract tests
  dist/tokens.css                      committed generated CSS variables
  dist/tokens.json                     committed nested JSON
  dist/tokens.ts                       committed typed object

packages/ui/
  package.json
  tsconfig.json
  vite.config.ts                       ESM library build and declarations
  src/index.ts                         public API only
  src/provider/*                       locale and interaction-mode context
  src/icons/*                          closed Puntiro icon registry
  src/components/<Name>/*              one component per focused directory
  src/patterns/<Name>/*                domain patterns without I/O
  src/styles/global.css                fonts, reset, focus, mode attributes
  src/styles/utilities.css             visually-hidden and shared helpers

apps/storybook/
  package.json
  .storybook/main.ts                   story and addon discovery
  .storybook/preview.tsx               globals and PuntiroProvider decorator
  .storybook/manager.ts                supported manager theming
  .storybook/theme.ts                  Puntiro manager/docs themes
  .storybook/vitest.setup.ts           story annotations for Vitest
  src/docs/*                           bilingual platform articles and blocks
  src/stories/Components/*             component CSF stories and docs copy
  src/stories/KioskPatterns/*          pattern CSF stories and docs copy
  src/stories/fixtures.ts              stable RU/EN warehouse sample data
  playwright.config.ts                 fixed Chromium visual environment
  tests/visual.spec.ts                 local screenshot comparisons
  tests/kiosk-contract.spec.ts         no-scroll and touch-target assertions
  tests/visual-cases.ts                explicit baseline manifest
```

---

### Task 1: Scaffold the exact pnpm workspace and empty Storybook build

**Files:**
- Create: `package.json`
- Create: `pnpm-workspace.yaml`
- Create: `pnpm-lock.yaml`
- Create: `tsconfig.base.json`
- Create: `eslint.config.js`
- Create: `vitest.config.ts`
- Create: `.gitignore`
- Create: `README.md`
- Create: `packages/ui/package.json`
- Create: `packages/ui/tsconfig.json`
- Create: `packages/ui/vite.config.ts`
- Create: `packages/ui/src/index.ts`
- Create: `apps/storybook/package.json`
- Create: `apps/storybook/.storybook/main.ts`
- Create: `apps/storybook/.storybook/preview.tsx`
- Create: `apps/storybook/src/stories/WorkspaceReady.stories.tsx`

**Interfaces:**
- Consumes: approved repository docs and exact dependency baseline above.
- Produces: workspace commands `pnpm typecheck`, `pnpm test`, `pnpm storybook`, `pnpm storybook:build`, and package names `@puntiro/ui`, `@puntiro/tokens`, `@puntiro/storybook`.

- [ ] **Step 1: Record the current negative baseline**

Run: `test -f package.json && exit 1 || exit 0`

Expected: exit 0, proving no pre-existing JavaScript workspace is being overwritten.

- [ ] **Step 2: Create root manifests with exact pins**

Create `package.json` with `private: true`, `type: "module"`, `packageManager: "pnpm@11.17.0"`, engines `node >=24 <25` and `pnpm 11.17.0`, and these root scripts:

```json
{
  "scripts": {
    "build": "pnpm tokens:build && pnpm --filter @puntiro/ui build && pnpm storybook:build",
    "typecheck": "pnpm -r typecheck",
    "lint": "eslint .",
    "test": "vitest run",
    "test:storybook": "vitest run --project=storybook",
    "test:visual": "playwright test --config apps/storybook/playwright.config.ts",
    "tokens:build": "pnpm --filter @puntiro/tokens build",
    "tokens:check": "node scripts/check-generated.mjs",
    "storybook": "pnpm --filter @puntiro/storybook storybook",
    "storybook:build": "pnpm --filter @puntiro/storybook build"
  }
}
```

Add every package in the dependency table with the exact version shown. Put React, fonts, React Aria, Lucide, Style Dictionary, and `vite-plugin-dts` in the package that consumes them; put shared test/build tools at root.

Create `README.md` with the project name, the statement `Puntiro Design System Platform`, prerequisites Node 24 and pnpm 11.17.0, and the initial `pnpm install`, `pnpm storybook`, and `pnpm storybook:build` commands. Task 14 expands it with the complete validation matrix.

Create `pnpm-workspace.yaml`:

```yaml
packages:
  - apps/*
  - packages/*

saveExact: true
engineStrict: true
```

- [ ] **Step 3: Add strict shared TypeScript and ESLint configuration**

Use `moduleResolution: "Bundler"`, `jsx: "react-jsx"`, `strict: true`, `exactOptionalPropertyTypes: true`, `noUncheckedIndexedAccess: true`, `verbatimModuleSyntax: true`, and `noEmit: true` in `tsconfig.base.json`. Configure ESLint flat config for TypeScript files, reject unused variables except names beginning with `_`, and ignore `dist`, `storybook-static`, `playwright-report`, and `test-results`.

Configure root Vitest with a Node `unit` project including `packages/**/*.test.{ts,tsx}` and `apps/storybook/src/**/*.test.{ts,tsx}`. Task 4 adds the separate browser project named `storybook`.

- [ ] **Step 4: Add the minimal UI library build**

Export this proof value from `packages/ui/src/index.ts`:

```ts
export const puntiroUiVersion = '0.1.0' as const;
```

Configure Vite library mode with `src/index.ts` as the entry, ESM output only, React and React DOM external, and `vite-plugin-dts` for declarations.

- [ ] **Step 5: Add the minimal Storybook application**

Configure `@storybook/react-vite`, docs, a11y, and Vitest addons. Add `WorkspaceReady.stories.tsx` with a single semantic `<main>` that renders `Puntiro UI 0.1.0` from the workspace package.

- [ ] **Step 6: Install and verify the scaffold**

Run:

```bash
pnpm install
pnpm typecheck
pnpm lint
pnpm storybook:build
```

Expected: exact lockfile is created; typecheck and lint exit 0; static Storybook builds to `apps/storybook/storybook-static`.

- [ ] **Step 7: Commit**

```bash
git add package.json pnpm-workspace.yaml pnpm-lock.yaml tsconfig.base.json eslint.config.js vitest.config.ts .gitignore README.md packages/ui apps/storybook
git commit -m "build: scaffold Puntiro design system workspace"
```

---

### Task 2: Build the DTCG token source and reproducible outputs

**Files:**
- Create: `packages/tokens/package.json`
- Create: `packages/tokens/sd.config.mjs`
- Create: `packages/tokens/src/reference/brand.tokens.json`
- Create: `packages/tokens/src/reference/type.tokens.json`
- Create: `packages/tokens/src/reference/scale.tokens.json`
- Create: `packages/tokens/src/semantic/product.tokens.json`
- Create: `packages/tokens/src/semantic/interaction.tokens.json`
- Create: `packages/tokens/src/component/core.tokens.json`
- Create: `packages/tokens/src/token-contract.test.ts`
- Create: `packages/tokens/dist/tokens.css`
- Create: `packages/tokens/dist/tokens.json`
- Create: `packages/tokens/dist/tokens.ts`
- Create: `scripts/check-generated.mjs`
- Modify: `packages/ui/package.json`
- Modify: `packages/ui/src/index.ts`

**Interfaces:**
- Consumes: approved brandbook values and Style Dictionary `usesDtcg: true`.
- Produces: `@puntiro/tokens/tokens.css`, `@puntiro/tokens/tokens.json`, and `@puntiro/tokens/tokens` TypeScript export.

- [ ] **Step 1: Write the failing token contract**

```ts
import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';

const readJson = (path: string) =>
  JSON.parse(readFileSync(new URL(path, import.meta.url), 'utf8')) as Record<string, unknown>;

describe('Puntiro token source', () => {
  it('preserves approved brand colors and touch dimensions', () => {
    const brand = readJson('./reference/brand.tokens.json');
    const scale = readJson('./reference/scale.tokens.json');
    expect(brand).toHaveProperty('reference.color.registerInk.$value', '#171914');
    expect(brand).toHaveProperty('reference.color.handoffOrange.$value', '#FF5A1F');
    expect(scale).toHaveProperty('reference.size.touch.minimum.$value.value', 64);
    expect(scale).toHaveProperty('reference.size.touch.comfortable.$value.value', 72);
  });
});
```

- [ ] **Step 2: Run the test to verify red**

Run: `pnpm exec vitest run packages/tokens/src/token-contract.test.ts`

Expected: FAIL because the DTCG source files do not exist.

- [ ] **Step 3: Create the complete reference and semantic token sets**

Use DTCG `$type`, `$value`, and `$description`. Preserve these approved values exactly:

```text
registerInk #171914; registerInkSoft #292C26
labelPaper #F2F0E8; labelPaperStrong #FFFDF6
warehouseSteel #8B9189; registrationLine #C8C8BE
handoffOrange #FF5A1F; handoffOrangeSoft #FFC7B3
statusOk #267A53; statusWarning #D08A00; statusDanger #C93D33
space unit 8px; touch 64px/72px; standard touch 44px
radius label/control/surface 4px/12px/24px
motion fast/confirm 120ms/180ms
fonts Onest and IBM Plex Mono
```

Semantic tokens must alias reference tokens for canvas, surface, text, border, action, selected, focus, success, warning, danger, and disabled roles. Component tokens are limited to Button, NumberInput, Dialog, ShipmentTaskCard, and PrinterPicker aliases required by the first wave.

- [ ] **Step 4: Configure deterministic Style Dictionary outputs**

Set `usesDtcg: true`, `showFileHeader: false`, CSS `outputReferences: true`, and a `puntiro` prefix. Register a TypeScript format that returns:

```ts
export const tokens = /* nested resolved dictionary */ as const;
export type PuntiroTokens = typeof tokens;
```

The build script must clean only `packages/tokens/dist`, rebuild CSS/JSON/TypeScript, and never modify `brandbook/`.

- [ ] **Step 5: Run the focused green checks**

Run:

```bash
pnpm --filter @puntiro/tokens build
pnpm exec vitest run packages/tokens/src/token-contract.test.ts
rg --fixed-strings -- "--puntiro-semantic-color-action-primary" packages/tokens/dist/tokens.css
rg --fixed-strings -- "#FF5A1F" packages/tokens/dist/tokens.json
```

Expected: all commands exit 0.

- [ ] **Step 6: Add generated-diff enforcement**

Implement `scripts/check-generated.mjs` to snapshot the exact bytes and filenames currently present in `packages/tokens/dist`, run the token build, and compare the rebuilt directory with that snapshot. Fail on changed, missing, or additional files. This works before the first commit and does not depend on Git staging state.

Run: `pnpm tokens:check`

Expected: PASS because a second build reproduces the same bytes.

- [ ] **Step 7: Export tokens to UI and commit**

Import `@puntiro/tokens/tokens.css` once from the UI style entry and add the token package to UI dependencies.

```bash
git add packages/tokens packages/ui/package.json packages/ui/src/index.ts scripts/check-generated.mjs pnpm-lock.yaml
git commit -m "feat: add Puntiro token pipeline"
```

---

### Task 3: Add PuntiroProvider, typed locale dictionaries, and interaction modes

**Files:**
- Create: `packages/ui/src/provider/types.ts`
- Create: `packages/ui/src/provider/copy.ts`
- Create: `packages/ui/src/provider/PuntiroProvider.tsx`
- Create: `packages/ui/src/provider/usePuntiro.ts`
- Create: `packages/ui/src/provider/PuntiroProvider.test.tsx`
- Create: `packages/ui/src/styles/global.css`
- Create: `packages/ui/src/styles/utilities.css`
- Modify: `packages/ui/src/index.ts`
- Modify: `apps/storybook/.storybook/preview.tsx`

**Interfaces:**
- Consumes: semantic token CSS from Task 2.
- Produces: `Locale = 'ru' | 'en'`, `InteractionMode = 'touch' | 'standard'`, `PuntiroProvider`, `usePuntiro()`, and typed `t(key)`.

- [ ] **Step 1: Write the failing provider tests**

```tsx
import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { PuntiroProvider, usePuntiro } from '../index';

function Probe() {
  const { locale, mode, t } = usePuntiro();
  return <span>{locale}|{mode}|{t('action.print')}</span>;
}

it('defaults to Russian touch mode', () => {
  const html = renderToStaticMarkup(<PuntiroProvider><Probe /></PuntiroProvider>);
  expect(html).toContain('lang="ru"');
  expect(html).toContain('data-interaction-mode="touch"');
  expect(html).toContain('ru|touch|Напечатать');
});

it('switches to English standard mode', () => {
  const html = renderToStaticMarkup(
    <PuntiroProvider locale="en" mode="standard"><Probe /></PuntiroProvider>,
  );
  expect(html).toContain('en|standard|Print');
});
```

- [ ] **Step 2: Run the tests to verify red**

Run: `pnpm exec vitest run packages/ui/src/provider/PuntiroProvider.test.tsx`

Expected: FAIL because the provider API is missing.

- [ ] **Step 3: Implement the provider and exact public types**

```ts
export type Locale = 'ru' | 'en';
export type InteractionMode = 'touch' | 'standard';
export interface PuntiroProviderProps {
  children: React.ReactNode;
  locale?: Locale;
  mode?: InteractionMode;
}
```

Wrap children with React Aria `I18nProvider` using `ru-RU` or `en-US`, expose a typed context, and render one root element with `lang`, `data-locale`, and `data-interaction-mode`. The context must throw `PuntiroProvider is missing` when consumed outside the provider.

Add complete UI keys for print, cancel, retry, close, select printer, increment, decrement, loading, offline, error, unknown result, places, and `placeCounter` in RU/EN. The type `TranslationKey` is the key union from the Russian dictionary; the English dictionary must `satisfies Record<TranslationKey, string>`.

- [ ] **Step 4: Define global mode CSS**

Import Onest weights 400/500/600/700 and IBM Plex Mono weights 500/700. Define `--puntiro-control-min-block-size` as 64 px in touch and 44 px in standard mode. Add a visible two-layer `:focus-visible` outline using semantic focus tokens and support `prefers-reduced-motion` by setting both motion variables to `0ms`.

- [ ] **Step 5: Connect Storybook globals**

Add toolbar globals `locale` with text items `RU`/`EN` and `interactionMode` with items `Touch`/`Standard`. Default to `ru` and `touch`. Wrap every story and docs page in `PuntiroProvider` through one preview decorator.

- [ ] **Step 6: Run green checks and commit**

Run:

```bash
pnpm exec vitest run packages/ui/src/provider/PuntiroProvider.test.tsx
pnpm typecheck
pnpm storybook:build
```

Expected: all commands exit 0.

```bash
git add packages/ui apps/storybook/.storybook/preview.tsx pnpm-lock.yaml
git commit -m "feat: add provider and localization foundation"
```

---

### Task 4: Brand the Storybook shell and create the bilingual documentation contract

**Files:**
- Create: `apps/storybook/.storybook/theme.ts`
- Create: `apps/storybook/.storybook/manager.ts`
- Create: `apps/storybook/.storybook/vitest.setup.ts`
- Create: `apps/storybook/vitest.config.ts`
- Create: `apps/storybook/src/docs/types.ts`
- Create: `apps/storybook/src/docs/defineDocumentation.ts`
- Create: `apps/storybook/src/docs/defineDocumentation.test.ts`
- Create: `apps/storybook/src/docs/ComponentDocsPage.tsx`
- Create: `apps/storybook/src/docs/DocsSection.tsx`
- Create: `apps/storybook/src/docs/MaturityBadge.tsx`
- Create: `apps/storybook/src/docs/docs.css`
- Create: `apps/storybook/public/puntiro-mark.svg`
- Modify: `apps/storybook/.storybook/main.ts`
- Modify: `apps/storybook/.storybook/preview.tsx`

**Interfaces:**
- Consumes: `PuntiroProvider`, locale global, approved logo asset.
- Produces: `Maturity`, `LocalizedDocumentation`, `defineDocumentation()`, and `createDocsPage()` used by every later CSF file.

- [ ] **Step 1: Write the failing documentation-schema test**

```ts
import { describe, expect, it } from 'vitest';
import { defineDocumentation } from './defineDocumentation';

const complete = {
  overview: 'Overview', anatomy: ['Root'], variants: ['Default'], states: ['Ready'],
  behavior: ['Press'], content: ['Use verbs'], accessibility: ['Named'], usage: ['Example'],
  do: ['Be clear'], dont: ['Hide actions'], changelog: ['0.1.0 Initial'],
};

it('requires complete RU and EN documentation', () => {
  expect(() => defineDocumentation({ ru: complete, en: { ...complete, do: [] } }))
    .toThrow('en.do must contain at least one entry');
});
```

- [ ] **Step 2: Run the test to verify red**

Run: `pnpm exec vitest run apps/storybook/src/docs/defineDocumentation.test.ts`

Expected: FAIL because the documentation helper does not exist.

- [ ] **Step 3: Implement the documentation schema**

Use these exact fields: `overview`, `anatomy`, `variants`, `states`, `behavior`, `content`, `accessibility`, `usage`, `do`, `dont`, `changelog`. Require non-empty strings and at least one item per list in both locales. Define `Maturity` as `draft | beta | stable | deprecated`.

`ComponentDocsPage` renders, in order: title, maturity, localized overview, Primary, Anatomy, Variants, States, Behavior, Content, Accessibility, Usage, Do/Don't, Controls, Stories, Changelog. Use supported Storybook Doc Blocks rather than manager DOM overrides.

- [ ] **Step 4: Implement supported Storybook theming**

Create manager theme with Register Ink chrome, Label Paper text surfaces, Handoff Orange brand accent, `Puntiro` title, and the approved mark. Create a separate light docs theme using Label Paper. Copy `brandbook/assets/puntiro-mark.svg` byte-for-byte to Storybook public assets.

Do not inject selectors targeting undocumented Storybook internals. `manager.ts` may call only supported `addons.setConfig({ theme })` APIs.

- [ ] **Step 5: Configure Storybook browser tests and strict a11y mode**

Create a Vitest Storybook project with `storybookTest({ configDir: 'apps/storybook/.storybook' })`, Chromium from `@vitest/browser-playwright`, browser mode enabled, and `.storybook/vitest.setup.ts` registering project annotations. Set the global a11y test mode to `error` so violations fail the browser-test command rather than appearing only in the addon panel.

- [ ] **Step 6: Run green checks and commit**

Run:

```bash
pnpm exec vitest run apps/storybook/src/docs/defineDocumentation.test.ts
pnpm storybook:build
```

Expected: schema test and static build pass.

```bash
git add apps/storybook
git commit -m "feat: brand Storybook documentation platform"
```

---

### Task 5: Publish Start and Foundations as bilingual live documentation

**Files:**
- Create: `apps/storybook/src/docs/content.ru.ts`
- Create: `apps/storybook/src/docs/content.en.ts`
- Create: `apps/storybook/src/docs/LocalizedArticle.tsx`
- Create: `apps/storybook/src/docs/Start.mdx`
- Create: `apps/storybook/src/docs/foundations/Brand.mdx`
- Create: `apps/storybook/src/docs/foundations/Color.mdx`
- Create: `apps/storybook/src/docs/foundations/Typography.mdx`
- Create: `apps/storybook/src/docs/foundations/Spacing.mdx`
- Create: `apps/storybook/src/docs/foundations/Sizing.mdx`
- Create: `apps/storybook/src/docs/foundations/Surface.mdx`
- Create: `apps/storybook/src/docs/foundations/Motion.mdx`
- Create: `apps/storybook/src/docs/foundations/Focus.mdx`
- Create: `apps/storybook/src/docs/foundations/Accessibility.mdx`
- Create: `apps/storybook/src/docs/foundations/Tokens.mdx`
- Create: `apps/storybook/src/docs/TokenGallery.tsx`
- Create: `apps/storybook/src/docs/TokenGallery.test.tsx`
- Create: `apps/storybook/src/docs/start.css`

**Interfaces:**
- Consumes: generated `tokens.json`, `PuntiroProvider`, docs styling, approved brandbook copy.
- Produces: localized article IDs `start`, `foundation.brand`, `foundation.color`, `foundation.typography`, `foundation.spacing`, `foundation.sizing`, `foundation.surface`, `foundation.motion`, `foundation.focus`, `foundation.accessibility`, and `foundation.tokens`.

- [ ] **Step 1: Write the failing TokenGallery contract**

```tsx
import { renderToStaticMarkup } from 'react-dom/server';
import { expect, it } from 'vitest';
import { PuntiroProvider } from '@puntiro/ui';
import { TokenGallery } from './TokenGallery';

it('renders token names and values from generated data', () => {
  const html = renderToStaticMarkup(
    <PuntiroProvider><TokenGallery group="color" /></PuntiroProvider>,
  );
  expect(html).toContain('semantic.color.action.primary');
  expect(html).toContain('#FF5A1F');
});
```

- [ ] **Step 2: Run the test to verify red**

Run: `pnpm exec vitest run apps/storybook/src/docs/TokenGallery.test.tsx`

Expected: FAIL because `TokenGallery` is missing.

- [ ] **Step 3: Implement typed bilingual article content**

The English object must `satisfies Record<keyof typeof ruContent, ArticleCopy>`. Populate both languages from specification sections 1–3, 7, 8, 9, and 14. Preserve the approved promise exactly in Russian: `Точное место. Ясная последовательность. Уверенная передача.` Translate it as `Exact place. Clear sequence. Confident handoff.`

- [ ] **Step 4: Implement the Start-page preflight layout**

Use one centered introduction with `max-width: 1120px`, `clamp(48px, 5vw, 72px)`, a two-action row, one 2 × 2 dense system map, and one live Button/StatusBadge preview added after those components exist. Until Task 6, the preview region renders a semantic text sample rather than a fake control. No GSAP, carousel, accordion, or inline image is added.

- [ ] **Step 5: Implement Foundation pages and galleries**

Each MDX page contains only `Meta`, `LocalizedArticle`, and the relevant live block. TokenGallery reads generated JSON, never duplicates token values, and supports groups `color | typography | spacing | sizing | radius | motion`.

- [ ] **Step 6: Run green checks and commit**

Run:

```bash
pnpm exec vitest run apps/storybook/src/docs/TokenGallery.test.tsx
pnpm storybook:build
```

Expected: test and build pass; all eleven docs entries are indexed.

```bash
git add apps/storybook/src/docs
git commit -m "docs: add Puntiro design system foundations"
```

---

### Task 6: Implement PuntiroIcon, Button, and IconButton

**Files:**
- Create: `packages/ui/src/icons/iconRegistry.ts`
- Create: `packages/ui/src/icons/PuntiroIcon.tsx`
- Create: `packages/ui/src/icons/PuntiroIcon.module.css`
- Create: `packages/ui/src/components/Button/Button.tsx`
- Create: `packages/ui/src/components/Button/Button.module.css`
- Create: `packages/ui/src/components/Button/Button.types.ts`
- Create: `packages/ui/src/components/IconButton/IconButton.tsx`
- Create: `packages/ui/src/components/IconButton/IconButton.module.css`
- Modify: `packages/ui/src/index.ts`
- Create: `apps/storybook/src/stories/Components/Button.stories.tsx`
- Create: `apps/storybook/src/stories/Components/IconButton.stories.tsx`
- Create: `apps/storybook/src/stories/Components/PuntiroIcon.stories.tsx`
- Create: `apps/storybook/src/stories/Components/button.docs.ts`
- Create: `apps/storybook/src/stories/Components/iconButton.docs.ts`
- Create: `apps/storybook/src/stories/Components/icon.docs.ts`
- Modify: `apps/storybook/src/docs/Start.mdx`

**Interfaces:**
- Consumes: provider mode, semantic tokens, docs contract.
- Produces: `IconName`, `PuntiroIcon`, `Button`, `ButtonProps`, `IconButton`, and `IconButtonProps`.

- [ ] **Step 1: Write failing interaction stories**

Define `Button/Primary` with a spy `onPress` and this play function:

```ts
play: async ({ canvasElement, args }) => {
  const canvas = within(canvasElement);
  const button = canvas.getByRole('button', { name: 'Напечатать' });
  await userEvent.click(button);
  await expect(args.onPress).toHaveBeenCalledOnce();
  await expect(button).toHaveAttribute('data-primary-action', 'true');
}
```

Define IconButton a11y coverage using the visible accessible name `Закрыть` even though only the icon is rendered.

- [ ] **Step 2: Run the stories to verify red**

Run: `pnpm test:storybook`

Expected: FAIL because the components are missing.

- [ ] **Step 3: Implement the closed icon registry**

Export only these names: `archive`, `check`, `chevronDown`, `close`, `connection`, `error`, `minus`, `plus`, `printer`, `refresh`, `search`, `warning`. Map them internally to Lucide and keep Lucide component types private.

- [ ] **Step 4: Implement exact Button APIs**

```ts
export type ButtonVariant = 'primary' | 'secondary' | 'danger' | 'ghost';
export interface ButtonProps {
  children: React.ReactNode;
  variant?: ButtonVariant;
  isDisabled?: boolean;
  isLoading?: boolean;
  loadingLabel?: string;
  iconBefore?: IconName;
  onPress?: () => void;
  type?: 'button' | 'submit' | 'reset';
}

export interface IconButtonProps {
  label: string;
  icon: IconName;
  variant?: Exclude<ButtonVariant, 'primary'>;
  isDisabled?: boolean;
  onPress?: () => void;
}
```

Wrap React Aria Button internally. Loading keeps the accessible name, blocks repeat press, and renders a non-decorative progress cue. Primary sets `data-primary-action="true"`. Do not forward arbitrary `className` or `style`.

- [ ] **Step 5: Add bilingual docs and complete state stories**

Add Default, Primary, Secondary, Danger, Ghost, FocusVisible, Pressed, Disabled, Loading, LongRussianText, English, Touch, and Standard stories. Mark the components `beta` until manual touch review.

Replace the temporary semantic preview on `apps/storybook/src/docs/Start.mdx` with a live Button and StatusBadge preview assembled from public `@puntiro/ui` exports.

- [ ] **Step 6: Run green checks and commit**

Run:

```bash
pnpm test:storybook
pnpm --filter @puntiro/ui build
pnpm storybook:build
```

Expected: all commands pass.

```bash
git add packages/ui/src/icons packages/ui/src/components packages/ui/src/index.ts apps/storybook/src/stories/Components
git commit -m "feat: add button and icon primitives"
```

---

### Task 7: Implement Surface, StatusBadge, InlineMessage, and ProgressIndicator

**Files:**
- Create: `packages/ui/src/components/Surface/Surface.tsx`
- Create: `packages/ui/src/components/Surface/Surface.types.ts`
- Create: `packages/ui/src/components/Surface/Surface.module.css`
- Create: `packages/ui/src/components/StatusBadge/StatusBadge.tsx`
- Create: `packages/ui/src/components/StatusBadge/StatusBadge.types.ts`
- Create: `packages/ui/src/components/StatusBadge/StatusBadge.module.css`
- Create: `packages/ui/src/components/InlineMessage/InlineMessage.tsx`
- Create: `packages/ui/src/components/InlineMessage/InlineMessage.types.ts`
- Create: `packages/ui/src/components/InlineMessage/InlineMessage.module.css`
- Create: `packages/ui/src/components/ProgressIndicator/ProgressIndicator.tsx`
- Create: `packages/ui/src/components/ProgressIndicator/ProgressIndicator.types.ts`
- Create: `packages/ui/src/components/ProgressIndicator/ProgressIndicator.module.css`
- Modify: `packages/ui/src/index.ts`
- Create: `apps/storybook/src/stories/Components/Surface.stories.tsx`
- Create: `apps/storybook/src/stories/Components/StatusBadge.stories.tsx`
- Create: `apps/storybook/src/stories/Components/InlineMessage.stories.tsx`
- Create: `apps/storybook/src/stories/Components/ProgressIndicator.stories.tsx`
- Create: `apps/storybook/src/stories/Components/feedback.docs.ts`

**Interfaces:**
- Consumes: semantic tokens, PuntiroIcon, docs contract.
- Produces: `Surface`, `StatusBadge`, `InlineMessage`, and `ProgressIndicator` with Puntiro-owned prop types.

- [ ] **Step 1: Write failing semantic stories**

```ts
play: async ({ canvasElement }) => {
  const canvas = within(canvasElement);
  await expect(canvas.getByRole('status')).toHaveTextContent('Готов к печати');
  await expect(canvas.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '2');
  await expect(canvas.getByRole('progressbar')).toHaveAttribute('aria-valuemax', '5');
}
```

- [ ] **Step 2: Run red**

Run: `pnpm test:storybook`

Expected: FAIL because the four exports are missing.

- [ ] **Step 3: Implement exact APIs**

```ts
export type FeedbackTone = 'neutral' | 'info' | 'success' | 'warning' | 'danger';
export interface StatusBadgeProps { label: string; tone?: FeedbackTone; }
export interface InlineMessageProps { title: string; children?: React.ReactNode; tone?: FeedbackTone; }
export interface ProgressIndicatorProps { value: number; max: number; label: string; counterText?: string; }
export interface SurfaceProps {
  children: React.ReactNode;
  as?: 'div' | 'section' | 'article';
  variant?: 'plain' | 'raised' | 'outlined';
  padding?: 'none' | 'compact' | 'comfortable';
}
```

`StatusBadge` always renders an icon plus text. `InlineMessage` uses `role="alert"` only for danger and `role="status"` otherwise. `ProgressIndicator` renders a native progress semantic and IBM Plex Mono counter. Surface has no selection or click behavior.

- [ ] **Step 4: Add RU/EN docs and visual states**

Cover monochrome, long content, reduced motion, each tone, zero progress, partial progress, and complete progress. Do not use color alone.

- [ ] **Step 5: Run green and commit**

```bash
pnpm test:storybook
pnpm --filter @puntiro/ui build
git add packages/ui apps/storybook/src/stories/Components
git commit -m "feat: add feedback and surface primitives"
```

---

### Task 8: Implement the accessible 1–100 NumberInput

**Files:**
- Create: `packages/ui/src/components/NumberInput/NumberInput.tsx`
- Create: `packages/ui/src/components/NumberInput/NumberInput.types.ts`
- Create: `packages/ui/src/components/NumberInput/NumberInput.module.css`
- Modify: `packages/ui/src/index.ts`
- Create: `apps/storybook/src/stories/Components/NumberInput.stories.tsx`
- Create: `apps/storybook/src/stories/Components/numberInput.docs.ts`

**Interfaces:**
- Consumes: React Aria NumberField, PuntiroProvider locale, IconButton visuals.
- Produces: `NumberInputProps` with controlled/uncontrolled values and clamped stepper behavior.

- [ ] **Step 1: Write failing boundary and keyboard stories**

```ts
play: async ({ canvasElement }) => {
  const canvas = within(canvasElement);
  const input = canvas.getByRole('spinbutton', { name: 'Количество мест' });
  await userEvent.clear(input);
  await userEvent.type(input, '100');
  await userEvent.keyboard('{ArrowUp}');
  await expect(input).toHaveValue(100);
  await userEvent.keyboard('{ArrowDown}');
  await expect(input).toHaveValue(99);
}
```

Add a second play test proving decrement is disabled at 1 and increment is disabled at 100.

- [ ] **Step 2: Run red**

Run: `pnpm test:storybook`

Expected: FAIL because NumberInput is missing.

- [ ] **Step 3: Implement the public API**

```ts
export interface NumberInputProps {
  label: string;
  value?: number;
  defaultValue?: number;
  onChange?: (value: number) => void;
  minValue?: number;
  maxValue?: number;
  step?: number;
  description?: string;
  errorMessage?: string;
  isInvalid?: boolean;
  isDisabled?: boolean;
}
```

Default `minValue=1`, `maxValue=100`, `step=1`. Use React Aria NumberField so press-and-hold, arrow keys, locale parsing, validation, and live announcements remain correct. Render large minus/input/plus regions and disable mouse-wheel mutation to avoid accidental warehouse changes.

- [ ] **Step 4: Add complete stories and docs**

Include Default, Controlled, Minimum, Maximum, Invalid, Disabled, Touch, Standard, RU, EN, and LongError. Document that high-count confirmation belongs to a future composition policy, not NumberInput.

- [ ] **Step 5: Run green and commit**

```bash
pnpm test:storybook
pnpm --filter @puntiro/ui build
git add packages/ui apps/storybook/src/stories/Components
git commit -m "feat: add accessible number input"
```

---

### Task 9: Implement Select and Dialog without leaking React Aria types

**Files:**
- Create: `packages/ui/src/components/Select/Select.tsx`
- Create: `packages/ui/src/components/Select/Select.types.ts`
- Create: `packages/ui/src/components/Select/Select.module.css`
- Create: `packages/ui/src/components/Dialog/Dialog.tsx`
- Create: `packages/ui/src/components/Dialog/Dialog.types.ts`
- Create: `packages/ui/src/components/Dialog/Dialog.module.css`
- Modify: `packages/ui/src/index.ts`
- Create: `apps/storybook/src/stories/Components/Select.stories.tsx`
- Create: `apps/storybook/src/stories/Components/Dialog.stories.tsx`
- Create: `apps/storybook/src/stories/Components/select.docs.ts`
- Create: `apps/storybook/src/stories/Components/dialog.docs.ts`

**Interfaces:**
- Consumes: Button, PuntiroIcon, provider locale and mode.
- Produces: `SelectOption`, `SelectProps`, `DialogProps`, and `DialogAction`.

- [ ] **Step 1: Write failing selection and focus-restoration stories**

```ts
play: async ({ canvasElement }) => {
  const canvas = within(canvasElement);
  await userEvent.click(canvas.getByRole('button', { name: 'Язык принтера' }));
  await userEvent.click(within(document.body).getByRole('option', { name: 'ZPL' }));
  await expect(canvas.getByRole('button', { name: 'Язык принтера' })).toHaveTextContent('ZPL');
}
```

For Dialog, open it, assert title/description, press Escape, and assert focus returns to the trigger.

- [ ] **Step 2: Run red**

Run: `pnpm test:storybook`

Expected: FAIL because both exports are missing.

- [ ] **Step 3: Implement stable Puntiro-owned APIs**

```ts
export interface SelectOption {
  id: string;
  label: string;
  description?: string;
  isDisabled?: boolean;
}

export interface SelectProps {
  label: string;
  items: readonly SelectOption[];
  selectedId?: string;
  defaultSelectedId?: string;
  onSelectionChange?: (id: string) => void;
  placeholder?: string;
  description?: string;
  errorMessage?: string;
  isInvalid?: boolean;
  isDisabled?: boolean;
}
```

```ts
export interface DialogAction {
  id: string;
  label: string;
  variant: ButtonVariant;
  onPress: (close: () => void) => void;
}
export interface DialogProps {
  trigger: React.ReactElement;
  title: string;
  description?: string;
  children: React.ReactNode;
  actions: readonly DialogAction[];
  isDismissible?: boolean;
}
```

Use React Aria DialogTrigger, ModalOverlay, Modal, Dialog, Heading, and focus management internally.

- [ ] **Step 4: Add docs and states**

Select covers placeholder, selected, disabled item, invalid, long option, keyboard and touch. Dialog covers confirmation, destructive, non-dismissible busy state, long RU/EN copy, Escape, focus trap, and reduced motion. State explicitly that PrinterPicker, not Select, handles kiosk printer choice.

- [ ] **Step 5: Run green and commit**

```bash
pnpm test:storybook
pnpm --filter @puntiro/ui build
git add packages/ui apps/storybook/src/stories/Components
git commit -m "feat: add select and dialog primitives"
```

---

### Task 10: Implement ShipmentTaskCard and ConnectivityBanner

**Files:**
- Create: `packages/ui/src/patterns/ShipmentTaskCard/ShipmentTaskCard.tsx`
- Create: `packages/ui/src/patterns/ShipmentTaskCard/ShipmentTaskCard.types.ts`
- Create: `packages/ui/src/patterns/ShipmentTaskCard/ShipmentTaskCard.module.css`
- Create: `packages/ui/src/patterns/ConnectivityBanner/ConnectivityBanner.tsx`
- Create: `packages/ui/src/patterns/ConnectivityBanner/ConnectivityBanner.types.ts`
- Create: `packages/ui/src/patterns/ConnectivityBanner/ConnectivityBanner.module.css`
- Modify: `packages/ui/src/index.ts`
- Create: `apps/storybook/src/stories/fixtures.ts`
- Create: `apps/storybook/src/stories/KioskPatterns/ShipmentTaskCard.stories.tsx`
- Create: `apps/storybook/src/stories/KioskPatterns/ConnectivityBanner.stories.tsx`
- Create: `apps/storybook/src/stories/KioskPatterns/shipmentTaskCard.docs.ts`
- Create: `apps/storybook/src/stories/KioskPatterns/connectivityBanner.docs.ts`

**Interfaces:**
- Consumes: Surface, StatusBadge, Button behavior, provider locale.
- Produces: `ShipmentTaskCardProps`, `ShipmentTaskStatus`, `ConnectivityBannerProps`, and `ConnectivityState`.

- [ ] **Step 1: Write failing domain stories**

```ts
play: async ({ canvasElement, args }) => {
  const canvas = within(canvasElement);
  const card = canvas.getByRole('button', { name: /Отгрузка SO-2026-000184/ });
  await expect(card).toHaveTextContent('SO-2026-000184');
  await userEvent.click(card);
  await expect(args.onOpen).toHaveBeenCalledOnce();
}
```

For offline state, assert `role="status"` contains last synchronization time and remaining offline allowance.

- [ ] **Step 2: Run red**

Run: `pnpm test:storybook`

Expected: FAIL because patterns are missing.

- [ ] **Step 3: Implement exact pattern data contracts**

```ts
export type ShipmentTaskStatus = 'ready' | 'updated' | 'attention';
export interface ShipmentTaskCardProps {
  shipmentNumber: string;
  receivedAt: Date;
  plannedShipAt?: Date;
  status: ShipmentTaskStatus;
  onOpen: () => void;
  isSelected?: boolean;
}

export type ConnectivityState = 'online' | 'offlineAllowed' | 'offlineExpired';
export interface ConnectivityBannerProps {
  state: ConnectivityState;
  lastSyncedAt: Date;
  offlineRemainingSeconds?: number;
}
```

The full shipment number remains in visible text and accessible name. Use `Intl.DateTimeFormat` from provider locale. The banner never uses a green dot without a text label.

Mark each pattern root with `data-kiosk-working-region` so the shared no-scroll contract can inspect it without depending on CSS class names.

- [ ] **Step 4: Add warehouse fixtures and edge stories**

Create fixed dates in UTC and numbers `SO-2026-000184` and `SALE-DOCUMENT-2026-08-07-000000987654321`. Cover updated-after-print, attention, long number, RU/EN, selected, online, offline allowed, and offline expired.

- [ ] **Step 5: Run green and commit**

```bash
pnpm test:storybook
pnpm --filter @puntiro/ui build
git add packages/ui apps/storybook/src/stories
git commit -m "feat: add shipment and connectivity patterns"
```

---

### Task 11: Implement PlaceCounter and PrinterPicker

**Files:**
- Create: `packages/ui/src/patterns/PlaceCounter/PlaceCounter.tsx`
- Create: `packages/ui/src/patterns/PlaceCounter/PlaceCounter.types.ts`
- Create: `packages/ui/src/patterns/PlaceCounter/PlaceCounter.module.css`
- Create: `packages/ui/src/patterns/PrinterPicker/PrinterPicker.tsx`
- Create: `packages/ui/src/patterns/PrinterPicker/PrinterPicker.types.ts`
- Create: `packages/ui/src/patterns/PrinterPicker/PrinterPicker.module.css`
- Modify: `packages/ui/src/index.ts`
- Create: `apps/storybook/src/stories/KioskPatterns/PlaceCounter.stories.tsx`
- Create: `apps/storybook/src/stories/KioskPatterns/PrinterPicker.stories.tsx`
- Create: `apps/storybook/src/stories/KioskPatterns/placeCounter.docs.ts`
- Create: `apps/storybook/src/stories/KioskPatterns/printerPicker.docs.ts`

**Interfaces:**
- Consumes: NumberInput, StatusBadge, Surface, provider locale.
- Produces: `PlaceCounterProps`, `PrinterLanguage`, `PrinterState`, `PrinterOption`, and `PrinterPickerProps`.

- [ ] **Step 1: Write failing touch interaction stories**

```ts
play: async ({ canvasElement, args }) => {
  const canvas = within(canvasElement);
  await userEvent.click(canvas.getByRole('button', { name: 'Увеличить' }));
  await expect(canvas.getByText('2 из 2')).toBeVisible();
  await expect(args.onChange).toHaveBeenLastCalledWith(2);
}
```

For PrinterPicker, tap `Zebra ZD421 — ZPL`, assert `onSelectionChange('zebra-zd421')`, and assert offline printers are disabled.

- [ ] **Step 2: Run red**

Run: `pnpm test:storybook`

Expected: FAIL because both patterns are missing.

- [ ] **Step 3: Implement exact contracts**

```ts
export interface PlaceCounterProps {
  value: number;
  onChange: (value: number) => void;
  minValue?: number;
  maxValue?: number;
  isDisabled?: boolean;
}

export type PrinterLanguage = 'zpl' | 'tspl';
export type PrinterState = 'ready' | 'busy' | 'offline' | 'error';
export interface PrinterOption {
  id: string;
  name: string;
  language: PrinterLanguage;
  state: PrinterState;
}
export interface PrinterPickerProps {
  printers: readonly PrinterOption[];
  selectedId?: string;
  onSelectionChange: (id: string) => void;
}
```

PlaceCounter renders the localized counter as `1 из 5` or `1 of 5` in IBM Plex Mono. PrinterPicker uses a large radio-group/listbox presentation rather than Select; only `ready` printers are selectable.

Mark both roots with `data-kiosk-working-region`; selectable ready-printer options must meet the active interaction-mode target size.

- [ ] **Step 4: Add edge stories and docs**

Cover 1, 10, 100, disabled, RU/EN, one printer, multiple printers, mixed ZPL/TSPL, busy, offline, error, no printers, long Windows queue name, keyboard arrows, and touch selection.

- [ ] **Step 5: Run green and commit**

```bash
pnpm test:storybook
pnpm --filter @puntiro/ui build
git add packages/ui apps/storybook/src/stories/KioskPatterns
git commit -m "feat: add place and printer patterns"
```

---

### Task 12: Implement PrintProgress and operational state patterns

**Files:**
- Create: `packages/ui/src/patterns/PrintProgress/PrintProgress.tsx`
- Create: `packages/ui/src/patterns/PrintProgress/PrintProgress.types.ts`
- Create: `packages/ui/src/patterns/PrintProgress/PrintProgress.module.css`
- Create: `packages/ui/src/patterns/EmptyState/EmptyState.tsx`
- Create: `packages/ui/src/patterns/EmptyState/EmptyState.types.ts`
- Create: `packages/ui/src/patterns/EmptyState/EmptyState.module.css`
- Create: `packages/ui/src/patterns/LoadingState/LoadingState.tsx`
- Create: `packages/ui/src/patterns/LoadingState/LoadingState.types.ts`
- Create: `packages/ui/src/patterns/LoadingState/LoadingState.module.css`
- Create: `packages/ui/src/patterns/ErrorState/ErrorState.tsx`
- Create: `packages/ui/src/patterns/ErrorState/ErrorState.types.ts`
- Create: `packages/ui/src/patterns/ErrorState/ErrorState.module.css`
- Create: `packages/ui/src/patterns/UnknownPrintResult/UnknownPrintResult.tsx`
- Create: `packages/ui/src/patterns/UnknownPrintResult/UnknownPrintResult.types.ts`
- Create: `packages/ui/src/patterns/UnknownPrintResult/UnknownPrintResult.module.css`
- Modify: `packages/ui/src/index.ts`
- Create: `apps/storybook/src/stories/KioskPatterns/PrintProgress.stories.tsx`
- Create: `apps/storybook/src/stories/KioskPatterns/SystemStates.stories.tsx`
- Create: `apps/storybook/src/stories/KioskPatterns/UnknownPrintResult.stories.tsx`
- Create: `apps/storybook/src/stories/KioskPatterns/printProgress.docs.ts`
- Create: `apps/storybook/src/stories/KioskPatterns/systemStates.docs.ts`
- Create: `apps/storybook/src/stories/KioskPatterns/unknownPrintResult.docs.ts`

**Interfaces:**
- Consumes: ProgressIndicator, InlineMessage, Button, Surface, StatusBadge.
- Produces: `PrintProgressProps`, `EmptyStateProps`, `LoadingStateProps`, `ErrorStateProps`, and `UnknownPrintResultProps`.

- [ ] **Step 1: Write failing print-state stories**

```ts
play: async ({ canvasElement, args }) => {
  const canvas = within(canvasElement);
  await expect(canvas.getByText('3 из 5')).toBeVisible();
  await expect(canvas.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '3');
  await userEvent.click(canvas.getByRole('button', { name: 'Повторить весь комплект' }));
  await expect(args.onRetryAll).toHaveBeenCalledOnce();
}
```

Add assertions that UnknownPrintResult has a warning heading, not `role="alert"` error copy, and exposes three distinct choices.

- [ ] **Step 2: Run red**

Run: `pnpm test:storybook`

Expected: FAIL because the patterns are missing.

- [ ] **Step 3: Implement exact contracts**

```ts
export interface PrintProgressProps {
  shipmentNumber: string;
  completed: number;
  total: number;
  printerName: string;
}

export interface UnknownPrintResultProps {
  shipmentNumber: string;
  placeCount: number;
  onRetryAll: () => void;
  onSelectPlaces: () => void;
  onResolveWithoutReprint: () => void;
}

export interface EmptyStateProps {
  title: string;
  description: string;
  action?: { label: string; onPress: () => void };
}
export interface LoadingStateProps { label: string; }
export interface ErrorStateProps {
  title: string;
  description: string;
  recoveryAction?: { label: string; onPress: () => void };
}
```

EmptyState accepts `title`, `description`, optional action. LoadingState accepts `label` and exposes `role="status"`. ErrorState accepts recovery action. UnknownPrintResult uses immutable explanatory copy: the system cannot prove whether all labels printed, so it never implies automatic success or failure.

Mark all pattern roots with `data-kiosk-working-region`. Recovery and retry buttons use the public Button so primary actions expose `data-primary-action="true"` consistently.

- [ ] **Step 4: Add all system-state stories and docs**

Cover no jobs, no archive results, loading, recoverable error, printer unavailable, unknown at 1 place, unknown at 100 places, RU/EN, long number, reduced motion, and disabled repeated actions.

- [ ] **Step 5: Run green and commit**

```bash
pnpm test:storybook
pnpm --filter @puntiro/ui build
git add packages/ui apps/storybook/src/stories/KioskPatterns
git commit -m "feat: add print and system state patterns"
```

---

### Task 13: Add Rules, Compositions guidance, local visual baselines, and kiosk invariants

**Files:**
- Create: `apps/storybook/src/docs/rules/Levels.mdx`
- Create: `apps/storybook/src/docs/rules/InteractionModes.mdx`
- Create: `apps/storybook/src/docs/rules/KioskLayout.mdx`
- Create: `apps/storybook/src/docs/rules/ActionHierarchy.mdx`
- Create: `apps/storybook/src/docs/rules/Content.mdx`
- Create: `apps/storybook/src/docs/rules/Feedback.mdx`
- Create: `apps/storybook/src/docs/rules/OfflineAndPrinting.mdx`
- Create: `apps/storybook/src/docs/rules/DoAndDont.mdx`
- Create: `apps/storybook/src/docs/compositions/Introduction.mdx`
- Create: `apps/storybook/src/docs/compositions/Contract.mdx`
- Create: `apps/storybook/playwright.config.ts`
- Create: `apps/storybook/tests/visual-cases.ts`
- Create: `apps/storybook/tests/visual.spec.ts`
- Create: `apps/storybook/tests/kiosk-contract.spec.ts`
- Create: `apps/storybook/tests/helpers.ts`
- Modify: `apps/storybook/src/docs/content.ru.ts`
- Modify: `apps/storybook/src/docs/content.en.ts`

**Interfaces:**
- Consumes: all Stable/Beta stories, fixed Storybook iframe URLs, approved composition rules.
- Produces: `VisualCase`, explicit screenshot manifest, and reusable `assertKioskContract(page)`.

- [ ] **Step 1: Write the failing kiosk contract test**

```ts
test('touch stories fit and expose 64px targets', async ({ page }) => {
  await openStory(page, 'kiosk-patterns-printerpicker--multiple-ready', 'ru', 'touch');
  await assertNoPageOverflow(page);
  await assertMinimumTargetSize(page, 64);
});
```

`assertNoPageOverflow` compares document and root scroll/client dimensions. `assertMinimumTargetSize` checks visible buttons, links, inputs, and option roles and prints the accessible name of any failing target.

Use this exact globals URL contract in `openStory`:

```ts
export async function openStory(
  page: Page,
  id: string,
  locale: 'ru' | 'en',
  mode: 'touch' | 'standard',
) {
  const globals = `locale:${locale};interactionMode:${mode}`;
  await page.goto(`/iframe.html?id=${id}&viewMode=story&globals=${encodeURIComponent(globals)}`);
  await page.waitForFunction(() => document.fonts.status === 'loaded');
}
```

`assertNoPageOverflow` evaluates `document.documentElement`, `document.body`, and every `[data-kiosk-working-region]`; it fails with the element name and both scroll/client dimensions when width or height differs.

- [ ] **Step 2: Run the test to verify red**

Run: `pnpm test:visual -- --grep "touch stories fit"`

Expected: FAIL because Playwright configuration and helpers are missing.

- [ ] **Step 3: Configure the pinned local browser environment**

Use Chromium only, viewport 1280 × 800, `deviceScaleFactor: 1`, `locale: ru-RU`, `timezoneId: Europe/Moscow`, `reducedMotion: reduce`, and `baseURL: http://127.0.0.1:6006`. Playwright's `webServer` runs `pnpm storybook -- --ci --no-open` and waits for that URL. Install the pinned browser with `pnpm exec playwright install chromium`.

- [ ] **Step 4: Define the explicit baseline matrix**

Include these story IDs in RU-touch and EN-touch: Button Primary, NumberInput Default, ShipmentTaskCard LongNumber, PrinterPicker MultipleReady, PrintProgress Partial, UnknownPrintResult Default. Include Button and Select in RU-standard and EN-standard. Do not snapshot every combinatorial story.

Each screenshot filename includes story ID, locale, and mode. Mask no product content; fixed dates and fixture values make output deterministic.

- [ ] **Step 5: Add tap and no-scroll contracts**

Run kiosk patterns with a Playwright context using `hasTouch: true`; tap the primary action and verify its callback-driven visible state. Assert no root or nested element marked `data-kiosk-working-region` scrolls. Assert a visible `[data-primary-action="true"]` where the story represents an actionable state.

- [ ] **Step 6: Add bilingual Rules and Compositions guidance**

Copy the approved Foundation → Component → Pattern → Composition definitions, 1280 × 800 constraints, action hierarchy, long-number rule, offline semantics, and unknown-result distinction. The Compositions section contains only contract and planned categories; it must not render a queue, dialog flow, archive screen, or other full application screen.

- [ ] **Step 7: Generate baselines, run green, and commit**

Run:

```bash
pnpm storybook:build
pnpm test:visual -- --update-snapshots
pnpm test:visual
```

Expected: all visual cases pass on the second run and kiosk contract reports zero overflow and zero undersized targets.

```bash
git add apps/storybook
git commit -m "test: add local visual and kiosk contracts"
```

---

### Task 14: Enforce maturity gates and complete the local handoff

**Files:**
- Create: `apps/storybook/src/docs/maturity-manifest.ts`
- Create: `apps/storybook/src/docs/maturity-manifest.test.ts`
- Modify: `apps/storybook/src/docs/Start.mdx`
- Modify: `apps/storybook/src/docs/ComponentDocsPage.tsx`
- Modify: `README.md`
- Modify: `.gitignore`
- Modify: `package.json`

**Interfaces:**
- Consumes: every component/pattern story meta and all automated checks.
- Produces: one maturity manifest, final root `pnpm check` command, and explicit automated/manual validation report format.

- [ ] **Step 1: Write the failing maturity-manifest test**

```ts
import { expect, it } from 'vitest';
import { maturityManifest } from './maturity-manifest';

it('lists every first-wave public export exactly once', () => {
  expect(maturityManifest.map((entry) => entry.name).sort()).toEqual([
    'Button', 'ConnectivityBanner', 'Dialog', 'EmptyState', 'ErrorState',
    'IconButton', 'InlineMessage', 'LoadingState', 'NumberInput', 'PlaceCounter',
    'PrinterPicker', 'PrintProgress', 'ProgressIndicator', 'PuntiroIcon', 'Select',
    'ShipmentTaskCard', 'StatusBadge', 'Surface', 'UnknownPrintResult',
  ].sort());
});
```

- [ ] **Step 2: Run red**

Run: `pnpm exec vitest run apps/storybook/src/docs/maturity-manifest.test.ts`

Expected: FAIL because the manifest is missing.

- [ ] **Step 3: Implement maturity enforcement**

Each entry contains `name`, `level`, `since`, `docsStoryId`, `manualTouchReviewed`, and `manualScreenReaderReviewed`. Initial levels are Beta because real Windows touch and screen-reader gates have not been completed. `ComponentDocsPage` shows the level and manual gate state without presenting automated checks as manual approval.

- [ ] **Step 4: Complete developer commands and handoff docs**

Document:

```bash
corepack enable
pnpm install --frozen-lockfile
pnpm storybook
pnpm tokens:check
pnpm typecheck
pnpm lint
pnpm test
pnpm test:storybook
pnpm storybook:build
pnpm test:visual
pnpm check
```

Define `pnpm check` as tokens check → typecheck → lint → unit tests → Storybook browser tests → static build → local visual tests. State that Windows 1280 × 800, real touch, gloves, motion review, screen reader, and physical printers remain manual gates.

- [ ] **Step 5: Run the complete fresh verification**

Run:

```bash
pnpm tokens:check
pnpm typecheck
pnpm lint
pnpm test
pnpm test:storybook
pnpm storybook:build
pnpm test:visual
git status --short
```

Expected: every automated command exits 0; `git status --short` shows only the files intentionally changed in Task 14 before commit. Record test counts and keep manual gates explicitly `not run`.

- [ ] **Step 6: Commit**

```bash
git add apps/storybook/src/docs README.md .gitignore package.json pnpm-lock.yaml
git commit -m "docs: finalize Puntiro design system platform"
```

## Final review checklist

- [ ] Every section of `2026-08-07-puntiro-design-system-platform-design.md` maps to at least one task above.
- [ ] No task introduces application API, persistence, routing, hardware I/O, or a full product screen.
- [ ] All 19 first-wave public components/patterns appear in Task 14's maturity manifest.
- [ ] Token values match the approved brandbook exactly.
- [ ] Component props use Puntiro-owned string unions and callbacks rather than React Aria or Lucide types.
- [ ] RU/EN is enforced by types and tested in stories.
- [ ] Touch/standard is applied through one provider and not repeated as component size props.
- [ ] Automated and manual validation are reported separately.
- [ ] Local visual baselines use fixed Chromium and committed fonts, with no paid cloud dependency.
- [ ] Future Compositions receive a documented contract without prematurely building screens.

## Primary references

- Storybook theming: <https://storybook.js.org/docs/configure/user-interface/theming>
- Storybook Docs and Doc Blocks: <https://storybook.js.org/docs/writing-docs>
- Storybook toolbar globals: <https://storybook.js.org/docs/essentials/toolbars-and-globals>
- Storybook Vitest addon: <https://storybook.js.org/docs/writing-tests/integrations/vitest-addon/>
- React Aria Components: <https://react-spectrum.adobe.com/react-aria/components.html>
- Style Dictionary DTCG configuration: <https://styledictionary.com/reference/config/>
- Playwright visual comparisons: <https://playwright.dev/docs/test-snapshots>
