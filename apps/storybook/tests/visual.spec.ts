import { expect, test } from '@playwright/test';
import { openStory } from './helpers';
import { visualCases, visualSnapshotName } from './visual-cases';

for (const visualCase of visualCases) {
  test(`${visualCase.id} · ${visualCase.locale} · ${visualCase.mode}`, async ({ page }) => {
    await openStory(page, visualCase.id, visualCase.locale, visualCase.mode);
    await expect(page).toHaveScreenshot(visualSnapshotName(visualCase), {
      animations: 'disabled',
    });
  });
}
