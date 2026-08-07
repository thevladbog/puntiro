import { readFileSync } from 'node:fs';
import { expect, it } from 'vitest';

const placeTypes = readFileSync(new URL('../../../../../packages/ui/src/patterns/PlaceCounter/PlaceCounter.types.ts', import.meta.url), 'utf8');
const printerTypes = readFileSync(new URL('../../../../../packages/ui/src/patterns/PrinterPicker/PrinterPicker.types.ts', import.meta.url), 'utf8');
const printerCss = readFileSync(new URL('../../../../../packages/ui/src/patterns/PrinterPicker/PrinterPicker.module.css', import.meta.url), 'utf8');

it('keeps PlaceCounter and PrinterPicker public APIs closed and library-owned', () => {
  for (const types of [placeTypes, printerTypes]) {
    expect(types).not.toContain('react-aria-components');
    expect(types).not.toMatch(/\bclassName\??:/);
    expect(types).not.toMatch(/\bstyle\??:/);
  }
});

it('uses semantic and PrinterPicker component tokens without reference leaks', () => {
  expect(printerCss).toContain('--puntiro-component-printer-picker-surface');
  expect(printerCss).toContain('--puntiro-component-printer-picker-selected');
  expect(printerCss).not.toContain('--puntiro-reference-');
});
