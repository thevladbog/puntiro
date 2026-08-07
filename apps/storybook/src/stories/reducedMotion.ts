import { expect } from 'storybook/test';

/**
 * Storybook's local, inherited reduced-motion test environment.
 * Use as `parameters: reducedMotionParameters`; the preview decorator adds
 * `data-puntiro-reduced-motion="true"`, which resolves both motion tokens to `0ms`.
 */

export const reducedMotionParameters = { puntiro: { reducedMotion: true } } as const;

export async function expectReducedMotionEnvironment(canvasElement: HTMLElement) {
  const environment = canvasElement.querySelector<HTMLElement>('[data-puntiro-reduced-motion="true"]');

  await expect(environment).not.toBeNull();
  const styles = getComputedStyle(environment!);

  await expect(styles.getPropertyValue('--puntiro-semantic-motion-fast').trim()).toBe('0ms');
  await expect(styles.getPropertyValue('--puntiro-semantic-motion-confirm').trim()).toBe('0ms');
}
