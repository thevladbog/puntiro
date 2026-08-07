import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { PuntiroProvider } from '../../provider/PuntiroProvider';
import { ProgressIndicator } from './ProgressIndicator';

describe('ProgressIndicator', () => {
  it('renders the native progress range and a mono counter', () => {
    const html = renderToStaticMarkup(<PuntiroProvider><ProgressIndicator value={2} max={5} label="Печать этикеток" /></PuntiroProvider>);

    expect(html).toContain('<progress');
    expect(html).toContain('aria-label="Печать этикеток"');
    expect(html).toContain('aria-valuemin="0"');
    expect(html).toContain('aria-valuenow="2"');
    expect(html).toContain('aria-valuemax="5"');
    expect(html).toContain('2 из 5');
  });

  it('clamps an invalid range to a valid native progress value', () => {
    const html = renderToStaticMarkup(<PuntiroProvider><ProgressIndicator value={8} max={0} label="Печать этикеток" /></PuntiroProvider>);

    expect(html).toContain('value="1"');
    expect(html).toContain('max="1"');
    expect(html).toContain('aria-valuenow="1"');
    expect(html).toContain('aria-valuemax="1"');
  });
});
