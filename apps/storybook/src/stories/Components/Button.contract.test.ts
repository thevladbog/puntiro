import { readFileSync } from 'node:fs';
import { expect, it } from 'vitest';

const css = readFileSync(new URL('../../../../../packages/ui/src/components/Button/Button.module.css', import.meta.url), 'utf8');
const iconButtonCss = readFileSync(new URL('../../../../../packages/ui/src/components/IconButton/IconButton.module.css', import.meta.url), 'utf8');
const tokens = JSON.parse(readFileSync(new URL('../../../../../packages/tokens/dist/tokens.json', import.meta.url), 'utf8')) as {
  semantic: { color: { text: { primary: string }; canvas: { default: string } } };
};

function luminance(hex: string): number {
  const channels = hex.slice(1).match(/.{2}/g)?.map((part) => Number.parseInt(part, 16) / 255) ?? [];
  const linear = channels.map((channel) => channel <= 0.04045 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4);
  return 0.2126 * linear[0]! + 0.7152 * linear[1]! + 0.0722 * linear[2]!;
}

it('uses a two-layer semantic focus treatment with a 3:1 inner boundary', () => {
  expect(css).toContain('.root:focus-visible');
  expect(css).toContain('outline: 2px solid var(--puntiro-semantic-color-text-primary);');
  expect(css).toContain('var(--puntiro-semantic-color-focus-ring)');
  expect(css).toContain('var(--puntiro-semantic-color-focus-offset)');

  const foreground = luminance(tokens.semantic.color.text.primary);
  const canvas = luminance(tokens.semantic.color.canvas.default);
  const contrast = (Math.max(foreground, canvas) + 0.05) / (Math.min(foreground, canvas) + 0.05);
  expect(contrast).toBeGreaterThanOrEqual(3);
});

it('uses semantic hover feedback without changing disabled controls', () => {
  expect(css).toContain(".root[data-hovered]:not([data-disabled])");
  expect(css).toContain('border-color: var(--puntiro-semantic-color-action-primary);');
  expect(iconButtonCss).toContain(".root[data-hovered]:not([data-disabled])");
  expect(iconButtonCss).toContain('border-color: var(--puntiro-semantic-color-action-primary);');
});
