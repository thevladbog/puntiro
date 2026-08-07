import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';

const readJson = (path: string) =>
  JSON.parse(readFileSync(new URL(path, import.meta.url), 'utf8')) as Record<string, unknown>;

describe('Puntiro token source', () => {
  it('preserves approved brand colors and touch dimensions', () => {
    const brand = readJson('./reference/brand.tokens.json');
    const scale = readJson('./reference/scale.tokens.json');

    expect(brand).toHaveProperty('reference.color.registerInk.$value', '#171914');
    expect(brand).toHaveProperty('reference.color.handoffOrange.$value', '#FF5A1F');
    expect(scale).toHaveProperty('reference.size.touch.minimum.$value.value', 64);
    expect(scale).toHaveProperty('reference.size.touch.comfortable.$value.value', 72);
  });
});
