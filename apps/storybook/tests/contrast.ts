export type Rgba = {
  red: number;
  green: number;
  blue: number;
  alpha: number;
};

const colorComponent = '(?:\\d+(?:\\.\\d+)?|\\.\\d+)';
const rgbPattern = new RegExp(`^rgb\\(\\s*(${colorComponent})\\s*,\\s*(${colorComponent})\\s*,\\s*(${colorComponent})\\s*\\)$`);
const rgbaPattern = new RegExp(`^rgba\\(\\s*(${colorComponent})\\s*,\\s*(${colorComponent})\\s*,\\s*(${colorComponent})\\s*,\\s*(${colorComponent})\\s*\\)$`);

function inRange(value: number, maximum: number): boolean {
  return Number.isFinite(value) && value >= 0 && value <= maximum;
}

export function parseCssColor(value: string): Rgba | null {
  if (value.trim() === 'transparent') return { red: 0, green: 0, blue: 0, alpha: 0 };

  const match = value.match(rgbaPattern) ?? value.match(rgbPattern);
  if (!match) return null;

  const [red, green, blue, alpha = 1] = match.slice(1).map(Number);
  if (![red, green, blue].every((channel) => inRange(channel, 255)) || !inRange(alpha, 1)) {
    return null;
  }

  return { red, green, blue, alpha };
}

function composite(foreground: Rgba, background: Rgba): Rgba {
  const alpha = foreground.alpha + background.alpha * (1 - foreground.alpha);
  if (alpha === 0) return { red: 0, green: 0, blue: 0, alpha: 0 };

  const channel = (foregroundChannel: number, backgroundChannel: number) => (
    foregroundChannel * foreground.alpha + backgroundChannel * background.alpha * (1 - foreground.alpha)
  ) / alpha;

  return {
    red: channel(foreground.red, background.red),
    green: channel(foreground.green, background.green),
    blue: channel(foreground.blue, background.blue),
    alpha
  };
}

export function resolveBackground(layers: readonly string[]): Rgba | null {
  const colors = layers.map(parseCssColor);
  if (colors.some((color) => color === null)) return null;

  const opaqueIndex = colors.findIndex((color) => color!.alpha === 1);
  if (opaqueIndex === -1) return null;

  let resolved = colors[opaqueIndex]!;
  for (let index = opaqueIndex - 1; index >= 0; index -= 1) {
    resolved = composite(colors[index]!, resolved);
  }
  return resolved;
}

function relativeLuminance(color: Rgba): number {
  const linear = (channel: number) => {
    const normalized = channel / 255;
    return normalized <= 0.04045 ? normalized / 12.92 : ((normalized + 0.055) / 1.055) ** 2.4;
  };
  return 0.2126 * linear(color.red) + 0.7152 * linear(color.green) + 0.0722 * linear(color.blue);
}

export function contrastRatio(foreground: Rgba, background: Rgba): number {
  if (background.alpha !== 1) throw new Error('Contrast background must be opaque');

  const displayedForeground = foreground.alpha === 1
    ? foreground
    : composite(foreground, background);
  const first = relativeLuminance(displayedForeground);
  const second = relativeLuminance(background);
  return (Math.max(first, second) + 0.05) / (Math.min(first, second) + 0.05);
}
