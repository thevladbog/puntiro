# Puntiro Token Color Gallery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the Storybook color-token table into a data-driven semantic palette plus an exhaustive swatch catalog without changing token values or public UI APIs.

**Architecture:** Keep `TokenGallery` as the only reader of the generated token artifact. Add small internal color renderers that receive resolved `TokenEntry` values, then reuse the same entries for the key-role gallery and complete table. Add an SSR contract for structure/data flow and a Playwright contract for the real 1280x800 Docs layout.

**Tech Stack:** React 19, TypeScript 6.0.3, generated DTCG token JSON, Storybook 10.5.3 Docs, Vitest 4.1.10, Playwright 1.61.1, CSS custom properties.

## Global Constraints

- The prominent gallery contains five individual roles and one grouped status-family card.
- The complete color table remains exhaustive; non-color token tables keep their current two-column structure.
- All displayed color values come from `packages/tokens/dist/tokens.json`; do not copy hex values into components, CSS, tests, or docs.
- Swatches are decorative and must remain paired with textual token names and resolved values.
- Light and dark swatches require visible boundaries against the Docs canvas.
- The Storybook Docs page has no horizontal overflow at the minimum supported 1280x800 viewport and adapts to wider viewports.
- Do not change `@puntiro/ui` exports, product components, token source values, or generated token formats.
- Existing component screenshot baselines remain byte-identical.

---

### Task 1: Data-driven semantic color gallery

**Files:**
- Modify: `apps/storybook/src/docs/TokenGallery.test.tsx`
- Modify: `apps/storybook/src/docs/TokenGallery.tsx`
- Modify: `apps/storybook/src/docs/start.css`

**Interfaces:**
- Consumes: `generatedTokens.semantic.color`, existing `TokenEntry { name: string; value: string }`, and `TokenGallery({ group }: { group: TokenGalleryGroup })`.
- Produces: internal `ColorSwatch`, `ColorRoleCard`, `StatusRoleCard`, and `KeyColorGallery` renderers; the public `TokenGallery` signature remains unchanged.

- [ ] **Step 1: Write the failing SSR contracts**

Extend `TokenGallery.test.tsx` with contracts that exercise the real generated artifact:

```tsx
it('renders the approved key roles and grouped status family from generated colors', () => {
  const html = renderToStaticMarkup(
    <PuntiroProvider><TokenGallery group="color" /></PuntiroProvider>,
  );

  for (const path of [
    'semantic.color.canvas.default',
    'semantic.color.surface.default',
    'semantic.color.text.primary',
    'semantic.color.action.primary',
    'semantic.color.selected.background',
    'semantic.color.success.default',
    'semantic.color.warning.default',
    'semantic.color.danger.default',
  ]) {
    expect(html).toContain(`data-token-path="${path}"`);
  }

  expect(html).toContain('data-color-role-card="status"');
  expect(html).toContain(generatedTokens.semantic.color.action.primary);
  expect(html).toContain(generatedTokens.semantic.color.warning.default);
});

it('adds a decorative swatch to every complete color-table row', () => {
  const html = renderToStaticMarkup(
    <PuntiroProvider><TokenGallery group="color" /></PuntiroProvider>,
  );
  const colorCount = collectLeafCount(generatedTokens.semantic.color);

  expect(html.match(/data-color-table-swatch="true"/g)).toHaveLength(colorCount);
  expect(html.match(/aria-hidden="true"/g)?.length).toBeGreaterThanOrEqual(colorCount);
  expect(html).toContain('<th scope="col">Preview</th>');
});

it('keeps non-color token groups on the two-column table', () => {
  const html = renderToStaticMarkup(
    <PuntiroProvider><TokenGallery group="sizing" /></PuntiroProvider>,
  );

  expect(html).not.toContain('data-key-color-gallery');
  expect(html).not.toContain('data-color-table-swatch');
  expect(html).not.toContain('<th scope="col">Preview</th>');
});
```

Add this test-only leaf counter above the tests so the expected swatch count is derived from the generated fixture rather than component code:

```ts
function collectLeafCount(value: unknown): number {
  if (typeof value === 'string' || typeof value === 'number') return 1;
  if (typeof value === 'object' && value !== null && 'value' in value && 'unit' in value) return 1;
  return Object.values(value as Record<string, unknown>)
    .reduce((total, child) => total + collectLeafCount(child), 0);
}
```

- [ ] **Step 2: Run the focused test and verify RED**

Run:

```bash
CI=true corepack pnpm exec vitest run --project=unit apps/storybook/src/docs/TokenGallery.test.tsx
```

Expected: the new tests fail because key-role cards, the status card, preview header, and color-row swatches do not exist.

- [ ] **Step 3: Implement resolved color entry selection**

In `TokenGallery.tsx`, keep `collectTokens` as the sole tree flattener and add exact path selection without reading another token source:

```ts
const keyColorPaths = [
  'semantic.color.canvas.default',
  'semantic.color.surface.default',
  'semantic.color.text.primary',
  'semantic.color.action.primary',
  'semantic.color.selected.background',
] as const;

const statusColorPaths = [
  'semantic.color.success.default',
  'semantic.color.warning.default',
  'semantic.color.danger.default',
] as const;

function requireToken(tokens: readonly TokenEntry[], name: string): TokenEntry {
  const token = tokens.find((entry) => entry.name === name);
  if (!token) throw new Error(`Missing documented color token: ${name}`);
  return token;
}
```

The explicit path lists define composition only; values still come from the generated artifact and a missing required role fails diagnostically.

- [ ] **Step 4: Implement small internal color renderers**

Add internal renderers in `TokenGallery.tsx`:

```tsx
function ColorSwatch({ token, table = false }: { token: TokenEntry; table?: boolean }) {
  return <span
    aria-hidden="true"
    className={table ? 'puntiro-token-swatch puntiro-token-swatch--table' : 'puntiro-token-swatch'}
    data-color-table-swatch={table ? 'true' : undefined}
    style={{ backgroundColor: token.value }}
  />;
}

function ColorRoleCard({ token }: { token: TokenEntry }) {
  const role = token.name.replace('semantic.color.', '');
  return <article className="puntiro-color-role" data-token-path={token.name}>
    <ColorSwatch token={token} />
    <div className="puntiro-color-role__copy">
      <strong>{role}</strong>
      <code>{token.name}</code>
      <code>{token.value}</code>
    </div>
  </article>;
}

function StatusRoleCard({ tokens }: { tokens: readonly TokenEntry[] }) {
  return <article className="puntiro-color-role puntiro-color-role--status" data-color-role-card="status">
    <div className="puntiro-color-role__status-swatches">
      {tokens.map((token) => <div key={token.name} data-token-path={token.name}>
        <ColorSwatch token={token} />
        <span>{token.name.replace('semantic.color.', '')}</span>
        <code>{token.value}</code>
      </div>)}
    </div>
  </article>;
}

function KeyColorGallery({ tokens }: { tokens: readonly TokenEntry[] }) {
  return <section className="puntiro-key-color-gallery" data-key-color-gallery aria-labelledby="puntiro-key-colors-title">
    <h2 id="puntiro-key-colors-title">Core semantic roles</h2>
    <div className="puntiro-key-color-gallery__grid">
      {keyColorPaths.map((path) => <ColorRoleCard key={path} token={requireToken(tokens, path)} />)}
      <StatusRoleCard tokens={statusColorPaths.map((path) => requireToken(tokens, path))} />
    </div>
  </section>;
}
```

Use semantic HTML; the swatch itself stays decorative while every role and value remains text.

- [ ] **Step 5: Render the gallery and conditional preview column**

Update `TokenGallery` so `group === 'color'` renders `KeyColorGallery` before the table. For color rows, render a `Preview` header and a swatch cell; for every other group, keep the exact existing two-column table:

```tsx
const isColor = group === 'color';

return <section className="puntiro-token-gallery" aria-label={`${group} tokens`}>
  {isColor ? <KeyColorGallery tokens={tokens} /> : null}
  <table>
    <thead><tr>
      {isColor ? <th scope="col">Preview</th> : null}
      <th scope="col">Token</th><th scope="col">Value</th>
    </tr></thead>
    <tbody>{tokens.map((token) => <tr key={token.name}>
      {isColor ? <td><ColorSwatch token={token} table /></td> : null}
      <th scope="row">{token.name}</th><td>{token.value}</td>
    </tr>)}</tbody>
  </table>
</section>;
```

- [ ] **Step 6: Add token-based responsive styling**

Extend `start.css` with:

```css
.puntiro-key-color-gallery { margin-bottom: 32px; }
.puntiro-key-color-gallery > h2 { margin-bottom: 16px; }
.puntiro-key-color-gallery__grid { display: grid; gap: 16px; grid-template-columns: repeat(3, minmax(0, 1fr)); }
.puntiro-color-role { background: var(--puntiro-semantic-color-surface-default); border: 1px solid var(--puntiro-semantic-color-border-default); border-radius: var(--puntiro-semantic-radius-surface); min-width: 0; overflow: hidden; }
.puntiro-token-swatch { border-bottom: 1px solid var(--puntiro-semantic-color-border-default); box-shadow: inset 0 0 0 1px var(--puntiro-semantic-color-focus-offset); display: block; min-height: 112px; }
.puntiro-color-role__copy { display: grid; gap: 6px; padding: 16px; }
.puntiro-color-role__copy code, .puntiro-color-role--status code { overflow-wrap: anywhere; }
.puntiro-color-role__status-swatches { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); }
.puntiro-color-role__status-swatches > div { display: grid; gap: 6px; min-width: 0; padding-bottom: 16px; }
.puntiro-color-role__status-swatches > div + div { border-left: 1px solid var(--puntiro-semantic-color-border-default); }
.puntiro-color-role__status-swatches .puntiro-token-swatch { margin-bottom: 10px; min-height: 112px; }
.puntiro-color-role__status-swatches span, .puntiro-color-role__status-swatches code { padding-inline: 10px; }
.puntiro-token-swatch--table { border: 1px solid var(--puntiro-semantic-color-border-default); border-radius: var(--puntiro-semantic-radius-control); min-height: 32px; width: 48px; }
.puntiro-token-gallery td:first-child { width: 64px; }

@media (max-width: 960px) { .puntiro-key-color-gallery__grid { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
@media (max-width: 700px) { .puntiro-key-color-gallery__grid { grid-template-columns: 1fr; } }
```

Integrate these rules with the existing media block rather than duplicate the `max-width: 700px` query. Use only existing generated semantic variables.

- [ ] **Step 7: Run focused and existing unit tests**

Run:

```bash
CI=true corepack pnpm exec vitest run --project=unit apps/storybook/src/docs/TokenGallery.test.tsx apps/storybook/src/docs/docsTokens.test.ts
```

Expected: PASS; color contracts are green and the Docs CSS semantic-token contract remains green.

- [ ] **Step 8: Commit Task 1**

```bash
git add apps/storybook/src/docs/TokenGallery.tsx apps/storybook/src/docs/TokenGallery.test.tsx apps/storybook/src/docs/start.css
git commit -m "feat: visualize color tokens"
```

---

### Task 2: Rendered Docs layout and visual regression

**Files:**
- Create: `apps/storybook/tests/token-gallery.spec.ts`
- Create: `apps/storybook/tests/token-gallery.spec.ts-snapshots/token-gallery-ru-darwin.png`

**Interfaces:**
- Consumes: the built-in Storybook Docs entry `foundations-color--docs`, `.puntiro-key-color-gallery`, `.puntiro-token-gallery`, and Playwright's configured 1280x800 Chromium viewport.
- Produces: a browser contract for gallery visibility, exact overflow behavior, and a new Docs-only visual baseline; it does not alter existing component cases.

- [ ] **Step 1: Write the rendered browser acceptance contract**

Create `apps/storybook/tests/token-gallery.spec.ts`:

```ts
import { expect, test } from '@playwright/test';

test('color token catalog renders the semantic gallery without horizontal overflow', async ({ page }) => {
  const globals = encodeURIComponent('locale:ru;interactionMode:touch');
  await page.goto(`/iframe.html?id=foundations-color--docs&viewMode=docs&globals=${globals}`);
  await page.waitForFunction(() => document.fonts.status === 'loaded');

  const gallery = page.locator('.puntiro-key-color-gallery');
  await expect(gallery).toBeVisible();
  await expect(gallery.locator('[data-token-path]')).toHaveCount(8);
  await expect(page.locator('[data-color-table-swatch="true"]')).toHaveCount(19);

  const overflow = await page.evaluate(() => ({
    html: [document.documentElement.scrollWidth, document.documentElement.clientWidth],
    body: [document.body.scrollWidth, document.body.clientWidth],
  }));
  expect(overflow.html[0]).toBe(overflow.html[1]);
  expect(overflow.body[0]).toBe(overflow.body[1]);

  await expect(page).toHaveScreenshot('token-gallery-ru.png', { animations: 'disabled' });
});
```

Before finalizing the literal swatch count, verify it against the current generated artifact. At the time of planning `semantic.color` contains 19 leaf tokens; a token-source change must deliberately update the browser fixture while the unit contract remains artifact-derived.

- [ ] **Step 2: Run the browser contract before accepting its visual baseline**

Run:

```bash
CI=true corepack pnpm exec playwright test --config apps/storybook/playwright.config.ts apps/storybook/tests/token-gallery.spec.ts
```

Expected after Task 1: structural and overflow assertions pass, while the command
fails only because the new approved screenshot baseline does not exist yet. Do
not update any pre-existing baseline.

- [ ] **Step 3: Run GREEN and create only the new approved baseline**

Run:

```bash
CI=true corepack pnpm exec playwright test --config apps/storybook/playwright.config.ts apps/storybook/tests/token-gallery.spec.ts --update-snapshots
CI=true corepack pnpm exec playwright test --config apps/storybook/playwright.config.ts apps/storybook/tests/token-gallery.spec.ts
```

Expected: first command creates one new token-gallery snapshot; the clean second pass is 1/1 green. Inspect the PNG at 1280x800 and reject it if text clips, light swatches disappear, the status labels wrap illegibly, or the table introduces horizontal page scrolling.

- [ ] **Step 4: Run the complete verification matrix**

Run:

```bash
CI=true corepack pnpm check
git diff --numstat -- apps/storybook/tests/visual.spec.ts-snapshots
git diff --check
```

Expected: token reproducibility, recursive typecheck, lint, unit tests, Storybook browser tests, static Storybook build, and all Playwright tests pass. `git diff --numstat` prints nothing for the 16 existing component baselines.

- [ ] **Step 5: Commit Task 2**

```bash
git add apps/storybook/tests/token-gallery.spec.ts apps/storybook/tests/token-gallery.spec.ts-snapshots
git commit -m "test: lock color token gallery layout"
```
