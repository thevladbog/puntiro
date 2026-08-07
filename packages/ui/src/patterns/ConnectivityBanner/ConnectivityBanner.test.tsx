import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { PuntiroProvider } from '../../provider/PuntiroProvider';
import { ConnectivityBanner } from './ConnectivityBanner';

describe('ConnectivityBanner', () => {
  it('announces an offline allowance with a deterministic upward minute rounding', () => {
    const html = renderToStaticMarkup(
      <PuntiroProvider>
        <ConnectivityBanner
          state="offlineAllowed"
          lastSyncedAt={new Date('2026-08-07T10:15:00.000Z')}
          offlineRemainingSeconds={61.9}
        />
      </PuntiroProvider>
    );

    expect(html).toContain('role="status"');
    expect(html).toContain('data-kiosk-working-region="true"');
    expect(html).toContain('Нет подключения');
    expect(html).toContain('Можно работать офлайн ещё 2 мин.');
    expect(html).toContain('Последняя синхронизация');
    expect(html).toContain('<svg');
  });

  it('normalizes an invalid offline allowance safely and localizes expiry through the provider', () => {
    const html = renderToStaticMarkup(
      <PuntiroProvider locale="en">
        <ConnectivityBanner
          state="offlineExpired"
          lastSyncedAt={new Date('2026-08-07T10:15:00.000Z')}
          offlineRemainingSeconds={Number.NaN}
        />
      </PuntiroProvider>
    );

    expect(html).toContain('Offline allowance expired');
    expect(html).toContain('Last sync');
    expect(html).toContain('<svg');
  });
});
