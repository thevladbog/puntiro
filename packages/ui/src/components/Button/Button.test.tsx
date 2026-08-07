import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { PuntiroProvider } from '../../provider/PuntiroProvider';
import { Button } from './Button';

describe('Button', () => {
  it('marks a primary action and preserves its label while loading', () => {
    const html = renderToStaticMarkup(<PuntiroProvider><Button variant="primary" isLoading loadingLabel="Печать выполняется">Напечатать</Button></PuntiroProvider>);
    expect(html).toContain('data-primary-action="true"');
    expect(html).toContain('Напечатать');
    expect(html).toContain('role="progressbar"');
    expect(html).toContain('aria-label="Печать выполняется"');
    expect(html).toContain('disabled=""');
  });

  it('uses the mode control size rather than accepting a style escape hatch', () => {
    const html = renderToStaticMarkup(<PuntiroProvider mode="standard"><Button>Сохранить</Button></PuntiroProvider>);
    expect(html).toContain('puntiro-control');
  });

  it('does not render an undefined class or primary-action attribute for secondary buttons', () => {
    const html = renderToStaticMarkup(<PuntiroProvider><Button variant="secondary">Сохранить</Button></PuntiroProvider>);
    expect(html).not.toContain('undefined');
    expect(html).not.toContain('data-primary-action');
  });
});
