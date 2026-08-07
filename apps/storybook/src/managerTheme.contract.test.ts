import { describe, expect, it } from 'vitest';
import { managerTheme } from '../.storybook/theme';

function relativeLuminance(hex: string): number {
  const channels = hex.slice(1).match(/.{2}/g);
  if (!channels) throw new Error(`Expected a hex color, received ${hex}`);

  const [red, green, blue] = channels.map((channel) => {
    const value = Number.parseInt(channel, 16) / 255;
    return value <= 0.04045 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4;
  });

  return 0.2126 * red + 0.7152 * green + 0.0722 * blue;
}

function contrast(foreground: string, background: string): number {
  const first = relativeLuminance(foreground);
  const second = relativeLuminance(background);
  return (Math.max(first, second) + 0.05) / (Math.min(first, second) + 0.05);
}

describe('managerTheme', () => {
  it('uses one coherent dark palette with WCAG AA text contrast', () => {
    expect(managerTheme.base).toBe('dark');
    expect(contrast(managerTheme.textColor, managerTheme.appBg)).toBeGreaterThanOrEqual(4.5);
    expect(contrast(managerTheme.textMutedColor, managerTheme.appBg)).toBeGreaterThanOrEqual(4.5);
    expect(contrast(managerTheme.textInverseColor, managerTheme.colorSecondary)).toBeGreaterThanOrEqual(4.5);
    expect(contrast(managerTheme.inputTextColor, managerTheme.inputBg)).toBeGreaterThanOrEqual(4.5);
    expect(contrast(managerTheme.buttonBorder, managerTheme.buttonBg)).toBeGreaterThanOrEqual(3);
  });
});
