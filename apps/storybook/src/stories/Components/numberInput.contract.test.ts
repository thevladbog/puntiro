import { readFileSync } from 'node:fs';
import { expect, it } from 'vitest';

const source = readFileSync(new URL('../../../../../packages/ui/src/components/NumberInput/NumberInput.tsx', import.meta.url), 'utf8');
const types = readFileSync(new URL('../../../../../packages/ui/src/components/NumberInput/NumberInput.types.ts', import.meta.url), 'utf8');
const css = readFileSync(new URL('../../../../../packages/ui/src/components/NumberInput/NumberInput.module.css', import.meta.url), 'utf8');

it('keeps NumberInput public types and styles free from implementation escape hatches', () => {
  expect(types).not.toContain('react-aria-components');
  expect(types).not.toMatch(/\bclassName\??:/);
  expect(types).not.toMatch(/\bstyle\??:/);
  expect(source).toContain('NumberField');
  expect(css).not.toContain('--puntiro-reference-');
});

it('uses a contrast-safe semantic text role for NumberInput errors', () => {
  expect(css).toMatch(/\.error\s*\{[^}]*color: var\(--puntiro-semantic-color-text-primary\)/s);
});
