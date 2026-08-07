import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { StatusBadgeVisual } from './StatusBadgeVisual';

describe('StatusBadgeVisual', () => {
  it('keeps the approved icon and text visual without creating a nested live region', () => {
    const html = renderToStaticMarkup(<StatusBadgeVisual tone="warning" label="Требует внимания" />);

    expect(html).not.toContain('role="status"');
    expect(html).toContain('Требует внимания');
    expect(html).toContain('<svg');
    expect(html).toContain('aria-hidden="true"');
  });
});
