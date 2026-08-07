import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { PuntiroProvider, usePuntiro } from '../index';

function Probe() {
  const { locale, mode, t } = usePuntiro();
  return <span>{locale}|{mode}|{t('action.print')}</span>;
}

describe('PuntiroProvider', () => {
  it('defaults to Russian touch mode', () => {
    const html = renderToStaticMarkup(
      <PuntiroProvider>
        <Probe />
      </PuntiroProvider>,
    );

    expect(html).toContain('lang="ru"');
    expect(html).toContain('data-interaction-mode="touch"');
    expect(html).toContain('ru|touch|Напечатать');
  });

  it('switches to English standard mode', () => {
    const html = renderToStaticMarkup(
      <PuntiroProvider locale="en" mode="standard">
        <Probe />
      </PuntiroProvider>,
    );

    expect(html).toContain('en|standard|Print');
  });

  it('throws a clear error outside the provider', () => {
    expect(() => renderToStaticMarkup(<Probe />)).toThrow('PuntiroProvider is missing');
  });
});
