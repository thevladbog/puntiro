import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { Surface } from './Surface';

describe('Surface', () => {
  it('renders its requested semantic container without interaction behavior', () => {
    const html = renderToStaticMarkup(<Surface as="article" variant="outlined" padding="compact">Содержимое поверхности</Surface>);

    expect(html).toContain('<article');
    expect(html).toContain('Содержимое поверхности');
    expect(html).not.toContain('role="button"');
    expect(html).not.toContain('tabindex=');
  });
});
