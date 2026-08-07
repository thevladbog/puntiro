import { expect, test } from '@playwright/test';

test('color token catalog renders the semantic gallery without horizontal overflow', async ({ page }) => {
  const globals = encodeURIComponent('locale:ru;interactionMode:touch');
  await page.goto(`/iframe.html?id=foundations-color--docs&viewMode=docs&globals=${globals}`);
  await page.waitForFunction(() => document.fonts.status === 'loaded');

  const gallery = page.locator('.puntiro-key-color-gallery');
  await expect(gallery).toBeVisible();
  await expect(gallery.locator('[data-token-path]')).toHaveCount(8);
  await expect(page.locator('[data-color-table-swatch="true"]')).toHaveCount(20);

  const overflow = await page.evaluate(() => ({
    html: [document.documentElement.scrollWidth, document.documentElement.clientWidth],
    body: [document.body.scrollWidth, document.body.clientWidth],
  }));
  expect(overflow.html[0]).toBe(overflow.html[1]);
  expect(overflow.body[0]).toBe(overflow.body[1]);

  await expect(page).toHaveScreenshot('token-gallery-ru.png', { animations: 'disabled' });
});
