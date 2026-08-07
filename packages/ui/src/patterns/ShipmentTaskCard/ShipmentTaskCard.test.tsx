import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { PuntiroProvider } from '../../provider/PuntiroProvider';
import { ShipmentTaskCard } from './ShipmentTaskCard';

describe('ShipmentTaskCard', () => {
  it('keeps a complete selected sales-document number in its accessible button and gives selection text', () => {
    const html = renderToStaticMarkup(
      <PuntiroProvider>
        <ShipmentTaskCard
          shipmentNumber="SALE-DOCUMENT-2026-08-07-000000987654321"
          receivedAt={new Date('2026-08-07T09:30:00.000Z')}
          plannedShipAt={new Date('2026-08-07T14:00:00.000Z')}
          status="updated"
          isSelected
          onOpen={() => undefined}
        />
      </PuntiroProvider>
    );

    expect(html).toContain('<button');
    expect(html).toContain('data-kiosk-working-region="true"');
    expect(html).toContain('aria-pressed="true"');
    expect(html).toContain('SALE-DOCUMENT-2026-08-07-000000987654321');
    expect(html).toContain('Данные обновлены после печати');
    expect(html).toContain('Выбрано');
  });

  it('uses the provider locale for the visible shipment labels and status', () => {
    const html = renderToStaticMarkup(
      <PuntiroProvider locale="en">
        <ShipmentTaskCard
          shipmentNumber="SO-2026-000184"
          receivedAt={new Date('2026-08-07T09:30:00.000Z')}
          status="attention"
          onOpen={() => undefined}
        />
      </PuntiroProvider>
    );

    expect(html).toContain('Shipment');
    expect(html).toContain('Needs attention');
    expect(html).toContain('Received');
  });
});
