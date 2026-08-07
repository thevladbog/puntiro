import { readFileSync } from 'node:fs';
import { expect, it } from 'vitest';
import { enContent } from './content.en';
import { ruContent } from './content.ru';
import { START_ACTIONS, StartLayout } from './StartLayout';
import { PuntiroProvider } from '@puntiro/ui';
import { createElement } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';

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

it('keeps Start navigation pointed at indexed Storybook documentation entries', () => {
  const html = renderToStaticMarkup(
    createElement(PuntiroProvider, null, createElement(StartLayout)),
  );
  const documentationIds = new Set(articles.map(([file]) => {
    const source = readFileSync(new URL(`./${file}`, import.meta.url), 'utf8');
    const title = source.match(/<Meta title="([^"]+)"/u)?.[1];

    expect(title).toBeTruthy();
    return `${title?.toLowerCase().replace('/', '-')}--docs`;
  }));

  for (const action of START_ACTIONS) {
    expect(documentationIds).toContain(action.id);
    expect(action.href).toBe(`./?path=/docs/${action.id}`);
    expect(html).toContain(`href="${action.href}"`);

    for (const [base, pathname] of [
      ['https://example.test/iframe.html?id=start--docs', '/'],
      ['https://example.test/subpath/iframe.html?id=start--docs', '/subpath/'],
    ]) {
      const resolved = new URL(action.href, base);
      expect(resolved.pathname).toBe(pathname);
      expect(resolved.searchParams.get('path')).toBe(`/docs/${action.id}`);
    }
  }
  expect(html).toContain('target="_top"');
});

it('assembles the Start preview from a provided live control', () => {
  const html = renderToStaticMarkup(
    createElement(PuntiroProvider, null, createElement(StartLayout, {
      preview: createElement('button', { type: 'button' }, 'Напечатать')
    })),
  );
  const source = readFileSync(new URL('./Start.mdx', import.meta.url), 'utf8');

  expect(html).toContain('<button type="button">Напечатать</button>');
  expect(source).toContain("import { Button } from '@puntiro/ui';");
  expect(source).toContain('<StartLayout preview={<Button');
  expect(source).not.toContain('Компоненты появятся здесь после их реализации.');
});

it('documents the localization and icon accessibility contracts in both locales', () => {
  const ruText = Object.values(ruContent).flatMap((article) => [article.lead, ...article.sections.map((section) => section.body)]).join(' ');
  const enText = Object.values(enContent).flatMap((article) => [article.lead, ...article.sections.map((section) => section.body)]).join(' ');

  expect(ruText).toContain('Русский является языком по умолчанию');
  expect(ruText).toContain('RU / EN');
  expect(ruText).toContain('PuntiroIcon');
  expect(ruText).toContain('доступное имя');
  expect(ruText).toContain('Эмодзи');
  expect(enText).toContain('Russian is the default language');
  expect(enText).toContain('RU / EN');
  expect(enText).toContain('PuntiroIcon');
  expect(enText).toContain('accessible name');
  expect(enText).toContain('Emoji');
});

it('keeps ordinary Russian prose localized', () => {
  const source = readFileSync(new URL('./content.ru.ts', import.meta.url), 'utf8');
  for (const term of ['landscape', 'Registration stem', 'controls', 'surfaces', 'Keyboard focus']) {
    expect(source).not.toContain(term);
  }
});
