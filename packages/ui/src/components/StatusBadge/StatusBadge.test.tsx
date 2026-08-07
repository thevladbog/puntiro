import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { StatusBadge } from './StatusBadge';

describe('StatusBadge', () => {
  it('announces a labelled success state with a decorative approved icon', () => {
    const html = renderToStaticMarkup(<StatusBadge tone="success" label="Готов к печати" />);

    expect(html).toContain('role="status"');
    expect(html).toContain('Готов к печати');
    expect(html).toContain('<svg');
    expect(html).toContain('aria-hidden="true"');
  });
});
