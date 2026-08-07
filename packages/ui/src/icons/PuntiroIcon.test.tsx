import { renderToStaticMarkup } from 'react-dom/server';
import { expect, it } from 'vitest';
import { iconNames } from './iconRegistry';
import { PuntiroIcon } from './PuntiroIcon';

it('keeps the public icon registry closed and renders decorative glyphs', () => {
  expect(iconNames).toEqual(['archive', 'check', 'chevronDown', 'close', 'connection', 'error', 'minus', 'plus', 'printer', 'refresh', 'search', 'warning']);
  const html = renderToStaticMarkup(<PuntiroIcon name="printer" />);
  expect(html).toContain('aria-hidden="true"');
});
