import { expect, test } from '@playwright/test';

test('standalone Docs receive Puntiro context and follow toolbar globals', async ({ page }) => {
  await page.goto('/?path=/docs/start--docs&globals=locale:ru;interactionMode:touch');

  const preview = page.locator('#storybook-preview-iframe').contentFrame();
  await expect(preview.locator('[data-locale="ru"][data-interaction-mode="touch"] .puntiro-article')).toBeVisible();
  await expect(preview.getByText('Точное место. Ясная последовательность. Уверенная передача.')).toBeVisible();

  await page.getByRole('button', { name: 'Locale for Puntiro content RU' }).click();
  await page.getByRole('option', { name: 'EN' }).click();
  await page.getByRole('button', { name: 'Interaction target size Touch' }).click();
  await page.getByRole('option', { name: 'Standard' }).click();

  await expect(preview.locator('[data-locale="en"][data-interaction-mode="standard"] .puntiro-article')).toBeVisible();
  await expect(preview.getByText('Exact place. Clear sequence. Confident handoff.')).toBeVisible();
});
