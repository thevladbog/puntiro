import { expect, test } from '@playwright/test';
import {
  assertKioskContract,
  assertMinimumTargetSize,
  assertNoPageOverflow,
  openStory,
} from './helpers';

test('touch stories fit and expose 64px targets', async ({ page }) => {
  await openStory(page, 'kiosk-patterns-printerpicker--multiple-ready', 'ru', 'touch');
  await assertNoPageOverflow(page);
  await assertMinimumTargetSize(page, 64);
});

test('touch primary action gives visible callback-driven feedback', async ({ page }) => {
  await openStory(page, 'kiosk-patterns-unknownprintresult--default', 'ru', 'touch');
  await assertKioskContract(page, { requiresPrimaryAction: true });

  const primaryAction = page.getByRole('button', { name: 'Повторить весь комплект' });
  await primaryAction.tap();
  await expect(primaryAction).toBeDisabled();
});

for (const locale of ['ru', 'en'] as const) {
  test(`long shipment number fits the kiosk contract in ${locale}`, async ({ page }) => {
    await openStory(page, 'kiosk-patterns-shipmenttaskcard--long-number', locale, 'touch');
    await assertKioskContract(page);
    await expect(page.getByRole('button', { name: /SALE-DOCUMENT-2026-08-07-000000987654321/ })).toBeVisible();
  });
}

test('ShipmentTaskCard exposes native hover and active feedback', async ({ page }) => {
  await openStory(page, 'kiosk-patterns-shipmenttaskcard--ready', 'ru', 'touch');
  const card = page.getByRole('button', { name: /Отгрузка SO-2026-000184/ });
  const initialBackground = await card.evaluate((element) => getComputedStyle(element).backgroundColor);
  const initialBorder = await card.evaluate((element) => getComputedStyle(element).borderColor);

  await card.hover();
  await expect.poll(() => card.evaluate((element) => getComputedStyle(element).backgroundColor)).not.toBe(initialBackground);

  const box = await card.boundingBox();
  expect(box, 'ShipmentTaskCard must have a tappable box').not.toBeNull();
  await page.mouse.move(box!.x + box!.width / 2, box!.y + box!.height / 2);
  await page.mouse.down();
  await expect.poll(() => card.evaluate((element) => getComputedStyle(element).borderColor)).not.toBe(initialBorder);
  await page.mouse.up();
});
