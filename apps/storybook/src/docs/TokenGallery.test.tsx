import { renderToStaticMarkup } from 'react-dom/server';
import { expect, it } from 'vitest';
import { PuntiroProvider } from '@puntiro/ui';
import { TokenGallery } from './TokenGallery';
import { tokenGalleryGroups } from './TokenGallery';
import generatedTokens from '../../../../packages/tokens/dist/tokens.json';

function collectLeafCount(value: unknown): number {
  if (typeof value === 'string' || typeof value === 'number') return 1;
  if (typeof value === 'object' && value !== null && 'value' in value && 'unit' in value) return 1;
  return Object.values(value as Record<string, unknown>)
    .reduce((total, child) => total + collectLeafCount(child), 0);
}

it('renders token names and values from generated data', () => {
  const html = renderToStaticMarkup(
    <PuntiroProvider><TokenGallery group="color" /></PuntiroProvider>,
  );

  expect(html).toContain('semantic.color.action.primary');
  expect(html).toContain('#FF5A1F');
});

it('exposes only the documented token group union', () => {
  expect(Object.keys(tokenGalleryGroups)).toEqual([
    'color', 'typography', 'spacing', 'sizing', 'radius', 'motion',
  ]);
});

it('includes the generated comfortable touch size in the sizing gallery', () => {
  const html = renderToStaticMarkup(
    <PuntiroProvider><TokenGallery group="sizing" /></PuntiroProvider>,
  );

  expect(html).toContain('reference.size.touch.comfortable');
  expect(html).toContain('72px');
});

it('renders the approved key roles and grouped status family from generated colors', () => {
  const html = renderToStaticMarkup(
    <PuntiroProvider><TokenGallery group="color" /></PuntiroProvider>,
  );

  for (const path of [
    'semantic.color.canvas.default',
    'semantic.color.surface.default',
    'semantic.color.text.primary',
    'semantic.color.action.primary',
    'semantic.color.selected.background',
    'semantic.color.success.default',
    'semantic.color.warning.default',
    'semantic.color.danger.default',
  ]) {
    expect(html).toContain(`data-token-path="${path}"`);
  }

  expect(html).toContain('data-color-role-card="status"');
  expect(html).toContain(generatedTokens.semantic.color.action.primary);
  expect(html).toContain(generatedTokens.semantic.color.warning.default);
});

it('adds a decorative swatch to every complete color-table row', () => {
  const html = renderToStaticMarkup(
    <PuntiroProvider><TokenGallery group="color" /></PuntiroProvider>,
  );
  const colorCount = collectLeafCount(generatedTokens.semantic.color);

  expect(html.match(/data-color-table-swatch="true"/g)).toHaveLength(colorCount);
  expect(html.match(/aria-hidden="true"/g)?.length).toBeGreaterThanOrEqual(colorCount);
  expect(html).toContain('<th scope="col">Preview</th>');
});

it('keeps non-color token groups on the two-column table', () => {
  const html = renderToStaticMarkup(
    <PuntiroProvider><TokenGallery group="sizing" /></PuntiroProvider>,
  );

  expect(html).not.toContain('data-key-color-gallery');
  expect(html).not.toContain('data-color-table-swatch');
  expect(html).not.toContain('<th scope="col">Preview</th>');
});
