import { expect, type Locator, type Page } from '@playwright/test';

export type StoryLocale = 'ru' | 'en';
export type StoryInteractionMode = 'touch' | 'standard';

export async function openStory(
  page: Page,
  id: string,
  locale: StoryLocale,
  mode: StoryInteractionMode,
) {
  const globals = `locale:${locale};interactionMode:${mode}`;
  await page.goto(`/iframe.html?id=${id}&viewMode=story&globals=${encodeURIComponent(globals)}`);
  await page.waitForFunction(() => document.fonts.status === 'loaded');
  await expect(page.locator('#storybook-root')).not.toBeEmpty();
}

type OverflowFailure = {
  element: string;
  scrollWidth: number;
  clientWidth: number;
  scrollHeight: number;
  clientHeight: number;
};

export async function assertNoPageOverflow(page: Page) {
  const failures = await page.evaluate<OverflowFailure[]>(() => {
    const elements = [
      document.documentElement,
      document.body,
      ...document.querySelectorAll<HTMLElement>('[data-kiosk-working-region]'),
    ];

    return elements.flatMap((element, index) => {
      const dimensions = {
        element: index === 0
          ? 'html'
          : index === 1
            ? 'body'
            : `${element.tagName.toLowerCase()}[data-kiosk-working-region]#${index - 1}`,
        scrollWidth: element.scrollWidth,
        clientWidth: element.clientWidth,
        scrollHeight: element.scrollHeight,
        clientHeight: element.clientHeight,
      };

      return dimensions.scrollWidth !== dimensions.clientWidth
        || dimensions.scrollHeight !== dimensions.clientHeight
        ? [dimensions]
        : [];
    });
  });

  expect(
    failures,
    failures.map((failure) => `${failure.element}: ${failure.scrollWidth}×${failure.scrollHeight} scroll / ${failure.clientWidth}×${failure.clientHeight} client`).join('\n'),
  ).toEqual([]);
}

async function accessibleName(target: Locator): Promise<string> {
  const ariaLabel = await target.getAttribute('aria-label');
  if (ariaLabel?.trim()) return ariaLabel.trim();

  return target.evaluate((element) => {
    if (element instanceof HTMLInputElement && element.labels?.length) {
      return Array.from(element.labels)
        .map((label) => label.innerText.trim())
        .filter(Boolean)
        .join(' ');
    }

    const labelledBy = element.getAttribute('aria-labelledby');
    if (labelledBy) {
      const label = labelledBy
        .split(/\s+/)
        .map((id) => document.getElementById(id)?.textContent?.trim() ?? '')
        .filter(Boolean)
        .join(' ');
      if (label) return label;
    }

    return element.textContent?.trim() || element.getAttribute('title')?.trim() || '';
  });
}

type TargetMetrics = {
  width: number;
  height: number;
};

async function targetMetrics(target: Locator): Promise<TargetMetrics | null> {
  return target.evaluate((element) => {
    const style = getComputedStyle(element);
    if (style.display === 'none' || style.visibility === 'hidden') return null;

    const isClippedInput = element instanceof HTMLInputElement
      && (style.clipPath !== 'none' || style.clip !== 'auto');
    const measurementTarget = isClippedInput && element.labels?.[0]
      ? element.labels[0]
      : element;
    const rect = measurementTarget.getBoundingClientRect();
    if (rect.width === 0 || rect.height === 0) return null;
    return { width: rect.width, height: rect.height };
  });
}

export async function assertMinimumTargetSize(page: Page, minimum: number) {
  const targets = page.locator('button, a[href], input, select, textarea, [role="option"]');
  const failures: string[] = [];

  for (let index = 0; index < await targets.count(); index += 1) {
    const target = targets.nth(index);
    const metrics = await targetMetrics(target);
    if (!metrics) continue;

    const name = await accessibleName(target);
    if (!name) failures.push(`unnamed target #${index}`);
    if (metrics.width < minimum || metrics.height < minimum) {
      failures.push(`${name || `unnamed target #${index}`}: ${metrics.width.toFixed(1)}×${metrics.height.toFixed(1)} px, expected at least ${minimum}×${minimum} px`);
    }
  }

  expect(failures, failures.join('\n')).toEqual([]);
}

export async function assertKioskContract(
  page: Page,
  options: { minimumTargetSize?: number; requiresPrimaryAction?: boolean } = {},
) {
  await assertNoPageOverflow(page);
  await assertMinimumTargetSize(page, options.minimumTargetSize ?? 64);

  if (options.requiresPrimaryAction) {
    await expect(page.locator('[data-primary-action="true"]')).toBeVisible();
  }
}
