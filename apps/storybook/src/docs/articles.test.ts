import { readFileSync } from 'node:fs';
import { expect, it } from 'vitest';
import { enContent } from './content.en';
import { ruContent } from './content.ru';

const articles = [
  ['Start.mdx', 'start'],
  ['foundations/Brand.mdx', 'foundation.brand'],
  ['foundations/Color.mdx', 'foundation.color'],
  ['foundations/Typography.mdx', 'foundation.typography'],
  ['foundations/Spacing.mdx', 'foundation.spacing'],
  ['foundations/Sizing.mdx', 'foundation.sizing'],
  ['foundations/Surface.mdx', 'foundation.surface'],
  ['foundations/Motion.mdx', 'foundation.motion'],
  ['foundations/Focus.mdx', 'foundation.focus'],
  ['foundations/Accessibility.mdx', 'foundation.accessibility'],
  ['foundations/Tokens.mdx', 'foundation.tokens'],
] as const;

it('provides complete bilingual copy for every article ID', () => {
  expect(Object.keys(ruContent)).toEqual(articles.map(([, id]) => id));
  expect(Object.keys(enContent)).toEqual(articles.map(([, id]) => id));
  expect(ruContent.start.lead).toBe('Точное место. Ясная последовательность. Уверенная передача.');
  expect(enContent.start.lead).toBe('Exact place. Clear sequence. Confident handoff.');
});

it('indexes every article as a minimal localized MDX document', () => {
  const main = readFileSync(new URL('../../.storybook/main.ts', import.meta.url), 'utf8');
  expect(main).toContain("'../src/docs/**/*.mdx'");

  for (const [file, id] of articles) {
    const source = readFileSync(new URL(`./${file}`, import.meta.url), 'utf8');
    expect(source).toContain('<Meta title=');
    expect(source).toContain(`<LocalizedArticle id="${id}"`);
  }
});
