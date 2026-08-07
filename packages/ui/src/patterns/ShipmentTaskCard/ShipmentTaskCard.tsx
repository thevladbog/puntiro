import { useId } from 'react';
import { StatusBadge } from '../../components/StatusBadge/StatusBadge';
import type { FeedbackTone } from '../../components/StatusBadge/StatusBadge.types';
import { usePuntiro } from '../../provider/usePuntiro';
import type { Locale } from '../../provider/types';
import type { ShipmentTaskCardProps, ShipmentTaskStatus } from './ShipmentTaskCard.types';
import styles from './ShipmentTaskCard.module.css';

type ShipmentTaskCopy = {
  shipment: string;
  received: string;
  planned: string;
  selected: string;
  statuses: Record<ShipmentTaskStatus, string>;
};

const shipmentTaskCopy: Record<Locale, ShipmentTaskCopy> = {
  ru: {
    shipment: 'Отгрузка',
    received: 'Получено',
    planned: 'Плановая отгрузка',
    selected: 'Выбрано',
    statuses: {
      ready: 'Готово к печати',
      updated: 'Данные обновлены после печати',
      attention: 'Требует внимания'
    }
  },
  en: {
    shipment: 'Shipment',
    received: 'Received',
    planned: 'Planned shipment',
    selected: 'Selected',
    statuses: {
      ready: 'Ready to print',
      updated: 'Updated after printing',
      attention: 'Needs attention'
    }
  }
};

const statusTone: Record<ShipmentTaskStatus, FeedbackTone> = {
  ready: 'success',
  updated: 'info',
  attention: 'warning'
};

function formatTimestamp(value: Date, locale: Locale): string {
  return new Intl.DateTimeFormat(locale === 'ru' ? 'ru-RU' : 'en-US', {
    dateStyle: 'medium',
    timeStyle: 'short',
    timeZone: 'UTC'
  }).format(value);
}

export function ShipmentTaskCard({
  shipmentNumber,
  receivedAt,
  plannedShipAt,
  status,
  onOpen,
  isSelected = false
}: ShipmentTaskCardProps) {
  const { locale } = usePuntiro();
  const copy = shipmentTaskCopy[locale];
  const statusId = useId();
  const className = [styles.root, isSelected ? styles.selected : ''].filter(Boolean).join(' ');

  return <button
    type="button"
    className={className}
    aria-pressed={isSelected}
    aria-describedby={statusId}
    data-kiosk-working-region="true"
    onClick={onOpen}
  >
    <span className={styles.heading}>
      <span className={styles.shipmentLabel}>{copy.shipment}</span>
      {isSelected ? <span className={styles.selectedLabel}>{copy.selected}</span> : null}
    </span>
    <span className={styles.number}>{shipmentNumber}</span>
    <span className={styles.timestamps}>
      <span><span className={styles.timestampLabel}>{copy.received}</span>{formatTimestamp(receivedAt, locale)}</span>
      {plannedShipAt ? <span><span className={styles.timestampLabel}>{copy.planned}</span>{formatTimestamp(plannedShipAt, locale)}</span> : null}
    </span>
    <span id={statusId}><StatusBadge tone={statusTone[status]} label={copy.statuses[status]} announce={false} /></span>
  </button>;
}
