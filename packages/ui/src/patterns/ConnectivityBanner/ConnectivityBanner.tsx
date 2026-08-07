import { PuntiroIcon } from '../../icons/PuntiroIcon';
import type { IconName } from '../../icons/iconRegistry';
import { usePuntiro } from '../../provider/usePuntiro';
import type { Locale } from '../../provider/types';
import type { ConnectivityBannerProps, ConnectivityState } from './ConnectivityBanner.types';
import styles from './ConnectivityBanner.module.css';

type ConnectivityCopy = {
  lastSync: string;
  online: string;
  offlineAllowed: string;
  offlineExpired: string;
  remaining: (minutes: number) => string;
};

const connectivityCopy: Record<Locale, ConnectivityCopy> = {
  ru: {
    lastSync: 'Последняя синхронизация',
    online: 'Подключение активно',
    offlineAllowed: 'Нет подключения',
    offlineExpired: 'Офлайн-лимит истёк',
    remaining: (minutes) => `Можно работать офлайн ещё ${minutes} мин.`
  },
  en: {
    lastSync: 'Last sync',
    online: 'Connection active',
    offlineAllowed: 'Offline',
    offlineExpired: 'Offline allowance expired',
    remaining: (minutes) => `Offline: ${minutes} min remaining`
  }
};

const stateIcon: Record<ConnectivityState, IconName> = {
  online: 'connection',
  offlineAllowed: 'warning',
  offlineExpired: 'error'
};

function formatTimestamp(value: Date, locale: Locale): string {
  return new Intl.DateTimeFormat(locale === 'ru' ? 'ru-RU' : 'en-US', {
    dateStyle: 'medium',
    timeStyle: 'short',
    timeZone: 'UTC'
  }).format(value);
}

function normalizeRemainingMinutes(seconds: number | undefined): number {
  const wholeSeconds = seconds !== undefined && Number.isFinite(seconds) ? Math.max(0, Math.floor(seconds)) : 0;
  return Math.ceil(wholeSeconds / 60);
}

export function ConnectivityBanner({ state, lastSyncedAt, offlineRemainingSeconds }: ConnectivityBannerProps) {
  const { locale } = usePuntiro();
  const copy = connectivityCopy[locale];
  const stateText = copy[state];
  const allowanceText = state === 'offlineAllowed' ? copy.remaining(normalizeRemainingMinutes(offlineRemainingSeconds)) : null;

  return <div className={[styles.root, styles[state]].join(' ')} role="status" data-kiosk-working-region="true">
    <PuntiroIcon name={stateIcon[state]} />
    <div className={styles.content}>
      <span className={styles.state}>{stateText}</span>
      {allowanceText ? <span>{allowanceText}</span> : null}
      <span><span className={styles.lastSync}>{copy.lastSync}</span>{formatTimestamp(lastSyncedAt, locale)}</span>
    </div>
  </div>;
}
