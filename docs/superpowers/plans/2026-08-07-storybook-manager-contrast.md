# Storybook Manager Contrast Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the Puntiro Storybook manager consistently readable by applying a coherent dark manager theme while preserving the light Docs and preview surfaces.

**Architecture:** Keep manager and Docs palettes independent while sharing only brand identity values such as fonts, radius, logo, and accent. Verify the supported Storybook theme values with a fast unit contrast contract and verify actual rendered manager states with Playwright against the manager UI.

**Tech Stack:** Storybook 10.5.3 theming API, TypeScript 6.0.3, Vitest 4.1.10, Playwright 1.61.1, WCAG 2.2 contrast rules.

## Global Constraints

- Manager normal text contrast is at least `4.5:1`; UI boundaries and focus indicators are at least `3:1`.
- Manager primary text is `#F2F0E8` on `#171914`; muted text is `#C8C8BE`; supported selected/accent roles use `#171914` on `#FF5A1F`.
- Manager controls use `#292C26` with `#FFFDF6` text.
- Manager branding uses `/puntiro-mark-on-dark.svg`; the light Docs theme keeps
  `/puntiro-mark.svg`.
- Component preview and Docs remain light; existing component screenshot baselines must not change.
- Use only Storybook's supported theming API; do not add CSS selectors targeting manager internals.
- Do not change Puntiro tokens, components, public APIs, stories, product screens, or hardware behavior.

## Approved Storybook-native selected-sidebar decision

This decision supersedes the selected-sidebar expectation in the historical
Task 1 snippets below. Storybook 10.5.3's supported dark-manager rendering
derives the selected sidebar background `rgb(194, 51, 0)` from Puntiro
`#FF5A1F` and applies light text `rgb(255, 255, 255)`. Accept that native
state when its rendered contrast is at least `4.5:1`; do not add
manager-internal CSS to substitute a different selected color.

The browser contract must assert this selected foreground/background pair and
the unselected `rgb(242, 240, 232)` on `rgb(23, 25, 20)` pair before measuring
contrast. This keeps the contract on stable rendered behavior rather than a
`.sidebar-item` implementation detail.

It must also rasterize the actual manager brand image and verify that at least
90% of fully opaque mark pixels reach `3:1` against the resolved dark manager
background, preventing the dark-ink mark from disappearing again.

---

### Task 1: Correct and lock the Storybook manager palette

**Files:**
- Create: `apps/storybook/src/managerTheme.contract.test.ts`
- Create: `apps/storybook/tests/manager-contrast.spec.ts`
- Modify: `apps/storybook/.storybook/theme.ts`

**Interfaces:**
- Consumes: `create()` from `storybook/theming`, existing `managerTheme` and `docsTheme` exports, the running Storybook manager route `/?path=/story/components-button--default`.
- Produces: the unchanged `managerTheme` and `docsTheme` exports, plus automated WCAG contrast coverage for configured and rendered manager states.

- [ ] **Step 1: Write the failing configured-theme contract**

Create `apps/storybook/src/managerTheme.contract.test.ts` with a small hex luminance helper and assertions against the real exported theme:

```ts
import { describe, expect, it } from 'vitest';
import { managerTheme } from '../.storybook/theme';

function relativeLuminance(hex: string): number {
  const channels = hex.slice(1).match(/.{2}/g);
  if (!channels) throw new Error(`Expected a hex color, received ${hex}`);

  const [red, green, blue] = channels.map((channel) => {
    const value = Number.parseInt(channel, 16) / 255;
    return value <= 0.04045 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4;
  });

  return 0.2126 * red + 0.7152 * green + 0.0722 * blue;
}

function contrast(foreground: string, background: string): number {
  const first = relativeLuminance(foreground);
  const second = relativeLuminance(background);
  return (Math.max(first, second) + 0.05) / (Math.min(first, second) + 0.05);
}

describe('managerTheme', () => {
  it('uses one coherent dark palette with WCAG AA text contrast', () => {
    expect(managerTheme.base).toBe('dark');
    expect(contrast(managerTheme.textColor, managerTheme.appBg)).toBeGreaterThanOrEqual(4.5);
    expect(contrast(managerTheme.textMutedColor, managerTheme.appBg)).toBeGreaterThanOrEqual(4.5);
    expect(contrast(managerTheme.textInverseColor, managerTheme.colorSecondary)).toBeGreaterThanOrEqual(4.5);
    expect(contrast(managerTheme.inputTextColor, managerTheme.inputBg)).toBeGreaterThanOrEqual(4.5);
    expect(contrast(managerTheme.buttonBorder, managerTheme.buttonBg)).toBeGreaterThanOrEqual(3);
  });
});
```

- [ ] **Step 2: Write the failing rendered-manager contract**

Create `apps/storybook/tests/manager-contrast.spec.ts`. The helper must read the computed foreground and walk ancestors until it finds the first non-transparent computed background, then calculate the WCAG ratio from the rendered RGB values.

```ts
import { expect, test, type Locator } from '@playwright/test';

async function expectTextContrast(locator: Locator, minimum = 4.5) {
  const colors = await locator.evaluate((element) => {
    const foreground = getComputedStyle(element).color;
    let current: Element | null = element;
    let background = 'rgba(0, 0, 0, 0)';

    while (current) {
      const candidate = getComputedStyle(current).backgroundColor;
      if (!candidate.endsWith(', 0)') && candidate !== 'transparent') {
        background = candidate;
        break;
      }
      current = current.parentElement;
    }

    return { foreground, background };
  });

  const channels = (value: string) => value.match(/[\d.]+/g)?.slice(0, 3).map(Number) ?? [];
  const luminance = (value: string) => {
    const [red, green, blue] = channels(value).map((channel) => {
      const normalized = channel / 255;
      return normalized <= 0.04045 ? normalized / 12.92 : ((normalized + 0.055) / 1.055) ** 2.4;
    });
    return 0.2126 * red + 0.7152 * green + 0.0722 * blue;
  };
  const foreground = luminance(colors.foreground);
  const background = luminance(colors.background);
  const ratio = (Math.max(foreground, background) + 0.05) / (Math.min(foreground, background) + 0.05);

  expect(ratio, `${colors.foreground} on ${colors.background}`).toBeGreaterThanOrEqual(minimum);
}

test('manager chrome keeps sidebar, toolbar, search and controls readable', async ({ page }) => {
  await page.goto('/?path=/story/components-button--default');

  await expectTextContrast(page.locator('a[href="/?path=/story/components-button--default"]'));
  await expectTextContrast(page.locator('a[href="/?path=/story/components-button--primary"]'));
  await expectTextContrast(page.getByPlaceholder('Find components'));
  await expectTextContrast(page.getByRole('tab', { name: 'Controls' }));
  await expectTextContrast(page.getByPlaceholder('Edit JSON string...'));
});
```

- [ ] **Step 3: Run both contracts to verify RED**

Run:

```bash
corepack pnpm exec vitest run --project=unit apps/storybook/src/managerTheme.contract.test.ts
corepack pnpm test:visual -- --grep "manager chrome"
```

Expected: the unit contract fails because `managerTheme.base` is `light`, primary manager text has `1.00:1` contrast on `appBg`, and the browser contract fails for selected/unselected sidebar items or the manager search field.

- [ ] **Step 4: Split shared identity from manager and Docs color roles**

Modify `apps/storybook/.storybook/theme.ts` so only identity values are shared and both palettes define their own semantic colors:

```ts
import { create } from 'storybook/theming';

const brandIdentity = {
  colorPrimary: '#FF5A1F',
  colorSecondary: '#FF5A1F',
  appBorderRadius: 12,
  fontBase: 'Onest, Segoe UI, Arial, sans-serif',
  fontCode: 'IBM Plex Mono, Cascadia Mono, Consolas, monospace',
  brandTitle: 'Puntiro',
  brandImage: '/puntiro-mark.svg'
};

export const managerTheme = create({
  ...brandIdentity,
  base: 'dark',
  appBg: '#171914',
  appContentBg: '#171914',
  appHoverBg: '#292C26',
  appPreviewBg: '#FFFDF6',
  appBorderColor: '#C8C8BE',
  textColor: '#F2F0E8',
  textInverseColor: '#171914',
  textMutedColor: '#C8C8BE',
  barTextColor: '#F2F0E8',
  barHoverColor: '#FFFDF6',
  barSelectedColor: '#FF5A1F',
  barBg: '#171914',
  buttonBg: '#292C26',
  buttonBorder: '#C8C8BE',
  booleanBg: '#8B9189',
  booleanSelectedBg: '#FF5A1F',
  inputBg: '#292C26',
  inputBorder: '#C8C8BE',
  inputTextColor: '#FFFDF6',
  inputBorderRadius: 12
});

export const docsTheme = create({
  ...brandIdentity,
  base: 'light',
  appBg: '#F2F0E8',
  appContentBg: '#FFFDF6',
  appHoverBg: '#F2F0E8',
  appPreviewBg: '#FFFDF6',
  appBorderColor: '#C8C8BE',
  textColor: '#171914',
  textInverseColor: '#FFFDF6',
  textMutedColor: '#8B9189',
  barBg: '#F2F0E8',
  barTextColor: '#171914',
  barHoverColor: '#171914',
  barSelectedColor: '#FF5A1F',
  buttonBg: '#FFFDF6',
  buttonBorder: '#C8C8BE',
  booleanBg: '#8B9189',
  booleanSelectedBg: '#FF5A1F',
  inputBg: '#FFFDF6',
  inputBorder: '#C8C8BE',
  inputTextColor: '#171914',
  inputBorderRadius: 12
});
```

- [ ] **Step 5: Run focused GREEN verification**

Run:

```bash
corepack pnpm exec vitest run --project=unit apps/storybook/src/managerTheme.contract.test.ts
corepack pnpm test:visual -- --grep "manager chrome"
```

Expected: both contracts pass; the selected sidebar link uses dark text on orange and unselected/sidebar/search/control text meets `4.5:1`.

- [ ] **Step 6: Verify the fix without accepting component baseline changes**

Run:

```bash
corepack pnpm --filter @puntiro/storybook build
CI=true corepack pnpm check
git status --short
git diff --check
```

Expected: the full check exits `0`; the 16 existing component visual baselines pass unchanged; Git shows only the theme, two new regression tests, and this plan/spec history. The pre-existing untracked `.pnpm-store/` remains untouched.

- [ ] **Step 7: Inspect live manager and commit**

Open `http://localhost:6006/?path=/story/components-button--default` after hot reload and verify the sidebar, toolbar, addon panel, Controls fields, hover, selected, and focus states are readable without changing the light preview.

```bash
git add apps/storybook/.storybook/theme.ts apps/storybook/src/managerTheme.contract.test.ts apps/storybook/tests/manager-contrast.spec.ts
git commit -m "fix: restore Storybook manager contrast"
```
