import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { PuntiroProvider } from '../../provider/PuntiroProvider';
import { PlaceCounter } from './PlaceCounter';

describe('PlaceCounter', () => {
  it('renders the controlled RU counter and kiosk marker', () => {
    const html = renderToStaticMarkup(
      <PuntiroProvider>
        <PlaceCounter value={1} maxValue={5} onChange={() => undefined} />
      </PuntiroProvider>
    );

    expect(html).toContain('data-kiosk-working-region="true"');
    expect(html).toContain('1 из 5');
    expect(html).toContain('Количество мест');
    expect(html).toContain('role="spinbutton"');
  });

  it('uses provider locale for the counter and label', () => {
    const html = renderToStaticMarkup(
      <PuntiroProvider locale="en">
        <PlaceCounter value={10} maxValue={100} onChange={() => undefined} />
      </PuntiroProvider>
    );

    expect(html).toContain('10 of 100');
    expect(html).toContain('Number of places');
  });
});
