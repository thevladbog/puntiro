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

test('manager brand mark remains visible against dark chrome', async ({ page }) => {
  await page.goto('/?path=/story/components-button--default');

  const brandMark = page.locator('img[alt="Puntiro"]');
  const { pixels, backgrounds } = await brandMark.evaluate(async (element) => {
    const image = element as HTMLImageElement;
    await image.decode();

    const canvas = document.createElement('canvas');
    canvas.width = image.naturalWidth;
    canvas.height = image.naturalHeight;
    const context = canvas.getContext('2d');
    if (!context) throw new Error('Expected a 2D canvas context');
    context.drawImage(image, 0, 0);

    const counts = new Map<string, number>();
    const data = context.getImageData(0, 0, canvas.width, canvas.height).data;
    for (let index = 0; index < data.length; index += 4) {
      if (data[index + 3] !== 255) continue;
      const key = `${data[index]},${data[index + 1]},${data[index + 2]}`;
      counts.set(key, (counts.get(key) ?? 0) + 1);
    }

    const backgrounds: string[] = [];
    let current: Element | null = element;
    while (current) {
      backgrounds.push(getComputedStyle(current).backgroundColor);
      current = current.parentElement;
    }

    return {
      pixels: [...counts].map(([rgb, count]) => ({ rgb: rgb.split(',').map(Number), count })),
      backgrounds
    };
  });

  const background = resolveBackground(backgrounds);
  if (!background) throw new Error(`Could not resolve brand background from: ${backgrounds.join(' -> ')}`);

  const totalPixels = pixels.reduce((total, pixel) => total + pixel.count, 0);
  const visiblePixels = pixels.reduce((total, pixel) => {
    const [red, green, blue] = pixel.rgb;
    return contrastRatio({ red, green, blue, alpha: 1 }, background) >= 3
      ? total + pixel.count
      : total;
  }, 0);

  expect(visiblePixels / totalPixels).toBeGreaterThanOrEqual(0.9);
});
