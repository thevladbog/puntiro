import { expect, test, type Locator } from '@playwright/test';
import { contrastRatio, parseCssColor, resolveBackground, type Rgba } from './contrast';

type TextColors = {
  foreground: string;
  background: Rgba;
  backgrounds: string[];
};

async function readTextColors(locator: Locator): Promise<TextColors> {
  const colors = await locator.evaluate((element) => {
    const foreground = getComputedStyle(element).color;
    let current: Element | null = element;
    const backgrounds: string[] = [];

    while (current) {
      backgrounds.push(getComputedStyle(current).backgroundColor);
      current = current.parentElement;
    }

    return { foreground, backgrounds };
  });

  const foreground = parseCssColor(colors.foreground);
  if (!foreground) throw new Error(`Could not parse foreground color: ${colors.foreground}`);
  const background = resolveBackground(colors.backgrounds);
  if (!background) throw new Error(`Could not resolve an opaque background from: ${colors.backgrounds.join(' -> ')}`);

  return { foreground: colors.foreground, background, backgrounds: colors.backgrounds };
}

function formatRgb(color: Rgba): string {
  return `rgb(${color.red}, ${color.green}, ${color.blue})`;
}

function expectTextContrast(colors: TextColors, minimum = 4.5) {
  const foreground = parseCssColor(colors.foreground);
  if (!foreground) throw new Error(`Could not parse foreground color: ${colors.foreground}`);
  const ratio = contrastRatio(foreground, colors.background);

  expect(ratio, `${colors.foreground} on ${colors.backgrounds.join(' -> ')}`).toBeGreaterThanOrEqual(minimum);
}

test('manager chrome keeps sidebar, toolbar, search and controls readable', async ({ page }) => {
  await page.goto('/?path=/story/components-button--default');

  const selectedStory = page.locator('a[href="/?path=/story/components-button--default"]');
  const unselectedStory = page.locator('a[href="/?path=/story/components-button--primary"]');
  const selectedColors = await readTextColors(selectedStory);
  const unselectedColors = await readTextColors(unselectedStory);

  expect(selectedColors.foreground).toBe('rgb(255, 255, 255)');
  expect(formatRgb(selectedColors.background)).toBe('rgb(194, 51, 0)');
  expect(unselectedColors.foreground).toBe('rgb(242, 240, 232)');
  expect(formatRgb(unselectedColors.background)).toBe('rgb(23, 25, 20)');
  expectTextContrast(selectedColors);
  expectTextContrast(unselectedColors);
  expectTextContrast(await readTextColors(page.getByPlaceholder('Find components')));
  expectTextContrast(await readTextColors(page.getByRole('tab', { name: 'Controls' })));
  expectTextContrast(await readTextColors(page.getByPlaceholder('Edit JSON string...')));
});
