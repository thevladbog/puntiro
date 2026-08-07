import { readFile } from 'node:fs/promises';
import { describe, expect, it } from 'vitest';

function variablesIn(css: string): Set<string> {
  return new Set(css.matchAll(/(--puntiro-[a-z0-9-]+)\s*:/g).map(([, variable]) => variable));
}

function tokenReferencesIn(css: string): string[] {
  return [...new Set(css.matchAll(/var\((--puntiro-[a-z0-9-]+)\)/g).map(([, variable]) => variable))];
}

describe('documentation styles', () => {
  it('reference only generated Puntiro token variables', async () => {
    const [docsCss, generatedTokensCss] = await Promise.all([
      readFile(new URL('./docs.css', import.meta.url), 'utf8'),
      readFile(new URL('../../../../packages/tokens/dist/tokens.css', import.meta.url), 'utf8')
    ]);
    const generatedVariables = variablesIn(generatedTokensCss);

    expect(tokenReferencesIn(docsCss).filter((variable) => !generatedVariables.has(variable))).toEqual([]);
  });
});
