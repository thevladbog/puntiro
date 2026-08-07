import { expect, test } from '@playwright/test';

test('preview recovers once when a stale story module fails to load', async ({ page }) => {
  let moduleRequests = 0;

  await page.route('**/src/stories/Components/Select.stories.tsx*', async (route) => {
    moduleRequests += 1;
    if (moduleRequests === 1) {
      await route.abort('failed');
      return;
    }

    await route.continue();
  });

  await page.goto('/?path=/story/components-select--placeholder');

  const preview = page.locator('#storybook-preview-iframe').contentFrame();
  await expect(preview.getByRole('button', { name: 'Язык принтера' })).toBeVisible();
  expect(moduleRequests).toBe(2);
});
