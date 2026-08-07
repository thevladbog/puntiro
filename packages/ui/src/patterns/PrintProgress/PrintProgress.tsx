import { ProgressIndicator } from '../../components/ProgressIndicator/ProgressIndicator';
import { StatusBadge } from '../../components/StatusBadge/StatusBadge';
import { Surface } from '../../components/Surface/Surface';
import { usePuntiro } from '../../provider/usePuntiro';
import type { Locale } from '../../provider/types';
import type { PrintProgressProps } from './PrintProgress.types';
import styles from './PrintProgress.module.css';

const copy: Record<Locale, {
  heading: string;
  printingStatus: string;
  completeStatus: string;
  shipment: string;
  printer: string;
  progress: string;
}> = {
  ru: {
    heading: 'Печать этикеток',
    printingStatus: 'Идёт печать',
    completeStatus: 'Комплект напечатан',
    shipment: 'Отгрузка',
    printer: 'Принтер',
    progress: 'Напечатано мест'
  },
  en: {
    heading: 'Printing labels',
    printingStatus: 'Printing',
    completeStatus: 'Set printed',
    shipment: 'Shipment',
    printer: 'Printer',
    progress: 'Places printed'
  }
};

export function PrintProgress({ shipmentNumber, completed, total, printerName }: PrintProgressProps) {
  const { locale } = usePuntiro();
  const localizedCopy = copy[locale];
  const isComplete = Number.isFinite(completed) && Number.isFinite(total) && total > 0 && completed >= total;

  return <div className={styles.root} data-kiosk-working-region="true">
    <Surface variant="outlined" padding="comfortable">
      <div className={styles.layout}>
        <div className={styles.header}>
          <h2 className={styles.heading}>{localizedCopy.heading}</h2>
          <StatusBadge
            label={isComplete ? localizedCopy.completeStatus : localizedCopy.printingStatus}
            tone={isComplete ? 'success' : 'info'}
          />
        </div>
        <dl className={styles.details}>
          <div>
            <dt>{localizedCopy.shipment}</dt>
            <dd className={styles.shipmentNumber}>{shipmentNumber}</dd>
          </div>
          <div>
            <dt>{localizedCopy.printer}</dt>
            <dd>{printerName}</dd>
          </div>
        </dl>
        <ProgressIndicator value={completed} max={total} label={localizedCopy.progress} />
      </div>
    </Surface>
  </div>;
}
