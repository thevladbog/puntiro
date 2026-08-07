import { expect, test, type Locator } from '@playwright/test';
import { contrastRatio, parseCssColor, resolveBackground } from './contrast';

const selectedSidebarBackground = 'rgb(194, 51, 0)';

async function expectTextContrast(locator: Locator, minimum = 4.5) {
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
  const ratio = contrastRatio(foreground, background);

  expect(ratio, `${colors.foreground} on ${colors.backgrounds.join(' -> ')}`).toBeGreaterThanOrEqual(minimum);
}

async function sidebarItemBackground(locator: Locator): Promise<string> {
  return locator.evaluate((element) => {
    const sidebarItem = element.closest('.sidebar-item');
    if (!sidebarItem) throw new Error('Expected story link to have a .sidebar-item ancestor');
    return getComputedStyle(sidebarItem).backgroundColor;
  });
}

test('manager chrome keeps sidebar, toolbar, search and controls readable', async ({ page }) => {
  await page.goto('/?path=/story/components-button--default');

  const selectedStory = page.locator('a[href="/?path=/story/components-button--default"]');
  const unselectedStory = page.locator('a[href="/?path=/story/components-button--primary"]');
  const selectedBackground = await sidebarItemBackground(selectedStory);
  const unselectedBackground = await sidebarItemBackground(unselectedStory);

  expect(selectedBackground).toBe(selectedSidebarBackground);
  expect(unselectedBackground).not.toBe(selectedSidebarBackground);
  await expectTextContrast(selectedStory);
  await expectTextContrast(unselectedStory);
  await expectTextContrast(page.getByPlaceholder('Find components'));
  await expectTextContrast(page.getByRole('tab', { name: 'Controls' }));
  await expectTextContrast(page.getByPlaceholder('Edit JSON string...'));
});
