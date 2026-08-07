import { describe, expect, it } from 'vitest';
import { contrastRatio, parseCssColor, resolveBackground } from '../tests/contrast';

describe('manager contrast color handling', () => {
  it('parses only finite in-range RGB and RGBA CSS colors', () => {
    expect(parseCssColor('rgb(23, 25, 20)')).toEqual({ red: 23, green: 25, blue: 20, alpha: 1 });
    expect(parseCssColor('rgba(255, 90, 31, 0.5)')).toEqual({ red: 255, green: 90, blue: 31, alpha: 0.5 });
    expect(parseCssColor('rgb(256, 0, 0)')).toBeNull();
    expect(parseCssColor('rgba(0, 0, nope, 1)')).toBeNull();
    expect(parseCssColor('rgba(0, 0, 0, NaN)')).toBeNull();
  });

  it('does not resolve fully transparent backgrounds without an opaque ancestor', () => {
    expect(resolveBackground(['rgba(0, 0, 0, 0)', 'transparent'])).toBeNull();
  });

  it('composites translucent layers over the resolved opaque ancestor before measuring contrast', () => {
    const background = resolveBackground(['rgba(255, 0, 0, 0.5)', 'rgb(0, 0, 255)']);

    expect(background).toEqual({ red: 127.5, green: 0, blue: 127.5, alpha: 1 });
    expect(contrastRatio({ red: 255, green: 255, blue: 255, alpha: 1 }, background!)).toBeGreaterThan(9);
  });
});
