import { readFileSync } from 'node:fs';
import { expect, it } from 'vitest';

const progressCss = readFileSync(new URL('../../../../../packages/ui/src/components/ProgressIndicator/ProgressIndicator.module.css', import.meta.url), 'utf8');
const taskSevenCss = [
  '../../../../../packages/ui/src/components/Surface/Surface.module.css',
  '../../../../../packages/ui/src/components/StatusBadge/StatusBadge.module.css',
  '../../../../../packages/ui/src/components/InlineMessage/InlineMessage.module.css',
  '../../../../../packages/ui/src/components/ProgressIndicator/ProgressIndicator.module.css'
].map((path) => readFileSync(new URL(path, import.meta.url), 'utf8'));
const tokens = JSON.parse(readFileSync(new URL('../../../../../packages/tokens/dist/tokens.json', import.meta.url), 'utf8')) as {
  semantic?: { color?: { progress?: { fill?: string; track?: string } } };
};

function luminance(hex: string): number {
  const channels = hex.slice(1).match(/.{2}/g)?.map((part) => Number.parseInt(part, 16) / 255) ?? [];
  const linear = channels.map((channel) => channel <= 0.04045 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4);
  return 0.2126 * linear[0]! + 0.7152 * linear[1]! + 0.0722 * linear[2]!;
}

it('uses semantic progress roles with at least 3:1 graphical contrast', () => {
  const fill = tokens.semantic?.color?.progress?.fill;
  const track = tokens.semantic?.color?.progress?.track;

  expect(fill).toBe('#171914');
  expect(track).toBe('#F2F0E8');
  expect(progressCss).toContain('var(--puntiro-semantic-color-progress-fill)');
  expect(progressCss).toContain('var(--puntiro-semantic-color-progress-track)');

  const contrast = (Math.max(luminance(fill!), luminance(track!)) + 0.05) / (Math.min(luminance(fill!), luminance(track!)) + 0.05);
  expect(contrast).toBeGreaterThanOrEqual(3);
});

it('keeps Task 7 component spacing on semantic roles', () => {
  for (const css of taskSevenCss) {
    expect(css).not.toContain('--puntiro-reference-space-');
  }
});
