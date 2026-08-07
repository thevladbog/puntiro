import { expect, test } from '@playwright/test';
import { createRequire } from 'node:module';

const require = createRequire(import.meta.url);
const generatedTokens = require('../../../packages/tokens/dist/tokens.json') as {
  semantic: { color: Record<string, unknown> };
};

interface ColorToken {
  path: string;
  value: string;
}

function collectColorTokens(value: unknown, path: string[] = ['semantic', 'color']): ColorToken[] {
  if (typeof value === 'string' || typeof value === 'number') {
    return [{ path: path.join('.'), value: String(value) }];
  }

  return Object.entries(value as Record<string, unknown>)
    .flatMap(([name, child]) => collectColorTokens(child, [...path, name]));
}

const individualRolePaths = [
  'semantic.color.canvas.default',
  'semantic.color.surface.default',
  'semantic.color.text.primary',
  'semantic.color.action.primary',
  'semantic.color.selected.background',
] as const;

const statusRolePaths = [
  'semantic.color.success.default',
  'semantic.color.warning.default',
  'semantic.color.danger.default',
] as const;

const expectedColorTokens = collectColorTokens(generatedTokens.semantic.color);

test('color token catalog renders the semantic gallery without horizontal overflow', async ({ page }) => {
  const globals = encodeURIComponent('locale:ru;interactionMode:touch');
  await page.goto(`/iframe.html?id=foundations-color--docs&viewMode=docs&globals=${globals}`);
  await page.waitForFunction(() => document.fonts.status === 'loaded');

  const gallery = page.locator('.puntiro-key-color-gallery');
  await expect(gallery).toBeVisible();
  for (const path of individualRolePaths) {
    await expect(gallery.locator(`.puntiro-color-role[data-token-path="${path}"]`)).toHaveCount(1);
  }

  const statusCard = gallery.locator('[data-color-role-card="status"]');
  await expect(statusCard).toHaveCount(1);
  for (const path of statusRolePaths) {
    await expect(statusCard.locator(`[data-token-path="${path}"]`)).toHaveCount(1);
  }

  const colorRows = page.locator('.puntiro-token-gallery table tbody > tr');
  await expect(colorRows).toHaveCount(expectedColorTokens.length);
  for (const token of expectedColorTokens) {
    const row = page.locator(`.puntiro-token-gallery table tbody > tr[data-color-token-row="${token.path}"]`);
    await expect(row).toHaveCount(1);
    await expect(row.locator('[data-color-table-swatch="true"]')).toHaveCount(1);
    await expect(row.locator('th[scope="row"]')).toHaveText(token.path);
    await expect(row.locator('td')).toHaveCount(2);
    await expect(row.locator('td').nth(1)).toHaveText(token.value);
  }

  const overflow = await page.evaluate(() => ({
    html: [document.documentElement.scrollWidth, document.documentElement.clientWidth],
    body: [document.body.scrollWidth, document.body.clientWidth],
  }));
  expect(overflow.html[0]).toBe(overflow.html[1]);
  expect(overflow.body[0]).toBe(overflow.body[1]);

  await expect(page).toHaveScreenshot('token-gallery-ru.png', { animations: 'disabled' });
});
