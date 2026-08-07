import { renderToStaticMarkup } from 'react-dom/server';
import { expect, it } from 'vitest';
import { PuntiroProvider } from '@puntiro/ui';
import { TokenGallery } from './TokenGallery';
import { tokenGalleryGroups } from './TokenGallery';

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
