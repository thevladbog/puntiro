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
