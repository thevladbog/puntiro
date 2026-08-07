import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { PuntiroProvider } from '../../provider/PuntiroProvider';
import { PrinterPicker } from './PrinterPicker';

describe('PrinterPicker', () => {
  it('renders a labeled kiosk radiogroup and disables every non-ready printer', () => {
    const html = renderToStaticMarkup(
      <PuntiroProvider>
        <PrinterPicker
          printers={[
            { id: 'zebra', name: 'Zebra ZD421', language: 'zpl', state: 'ready' },
            { id: 'tsc', name: 'TSC TE200', language: 'tspl', state: 'busy' },
            { id: 'bixolon', name: 'Bixolon XD5', language: 'zpl', state: 'offline' },
            { id: 'error', name: 'TSC MH241', language: 'tspl', state: 'error' }
          ]}
          onSelectionChange={() => undefined}
        />
      </PuntiroProvider>
    );

    expect(html).toContain('role="radiogroup"');
    expect(html).toContain('data-kiosk-working-region="true"');
    expect(html).toContain('Выберите принтер');
    expect(html).toContain('Zebra ZD421');
    expect(html).toContain('ZPL');
    expect(html.match(/disabled=""/g)).toHaveLength(3);
    expect(html).toContain('aria-describedby=');
    expect(html).not.toContain('role="status"');
  });

  it('localizes states and the empty list through the provider', () => {
    const html = renderToStaticMarkup(
      <PuntiroProvider locale="en">
        <PrinterPicker printers={[]} onSelectionChange={() => undefined} />
      </PuntiroProvider>
    );

    expect(html).toContain('Select a printer');
    expect(html).toContain('No printers found');
  });
});
