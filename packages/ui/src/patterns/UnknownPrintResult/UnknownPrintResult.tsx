import { useRef, useState } from 'react';
import { Button } from '../../components/Button/Button';
import { StatusBadge } from '../../components/StatusBadge/StatusBadge';
import { Surface } from '../../components/Surface/Surface';
import { usePuntiro } from '../../provider/usePuntiro';
import type { Locale } from '../../provider/types';
import type { UnknownPrintResultProps } from './UnknownPrintResult.types';
import styles from './UnknownPrintResult.module.css';

const copy: Record<Locale, {
  heading: string;
  status: string;
  explanation: string;
  shipment: string;
  place: (value: number) => string;
  retryAll: string;
  selectPlaces: string;
  resolveWithoutReprint: string;
}> = {
  ru: {
    heading: 'Результат печати неизвестен',
    status: 'Требуется решение',
    explanation: 'Система не может подтвердить, что все этикетки комплекта напечатаны. Повторной печати автоматически не будет — выберите, как продолжить.',
    shipment: 'Отгрузка',
    place: (value) => {
      const plural = new Intl.PluralRules('ru-RU').select(value);
      const unit = plural === 'one' ? 'место' : plural === 'few' ? 'места' : 'мест';
      return `${new Intl.NumberFormat('ru-RU').format(value)} ${unit}`;
    },
    retryAll: 'Повторить весь комплект',
    selectPlaces: 'Выбрать места',
    resolveWithoutReprint: 'Продолжить без повторной печати'
  },
  en: {
    heading: 'Print result unknown',
    status: 'Decision required',
    explanation: 'The system cannot confirm that every label in the set printed. Nothing will be reprinted automatically — choose how to continue.',
    shipment: 'Shipment',
    place: (value) => `${new Intl.NumberFormat('en-US').format(value)} ${value === 1 ? 'place' : 'places'}`,
    retryAll: 'Retry the entire set',
    selectPlaces: 'Select places',
    resolveWithoutReprint: 'Continue without reprinting'
  }
};

export function UnknownPrintResult({
  shipmentNumber,
  placeCount,
  onRetryAll,
  onSelectPlaces,
  onResolveWithoutReprint
}: UnknownPrintResultProps) {
  const { locale } = usePuntiro();
  const localizedCopy = copy[locale];
  const resolvedRef = useRef(false);
  const [isResolved, setIsResolved] = useState(false);

  function resolve(callback: () => void) {
    if (resolvedRef.current) return;
    resolvedRef.current = true;
    setIsResolved(true);
    callback();
  }

  return <section className={styles.root} data-kiosk-working-region="true">
    <Surface variant="outlined" padding="comfortable">
      <div className={styles.layout}>
        <div className={styles.header}>
          <h2>{localizedCopy.heading}</h2>
          <StatusBadge label={localizedCopy.status} tone="warning" />
        </div>
        <p className={styles.explanation}>{localizedCopy.explanation}</p>
        <dl className={styles.details}>
          <div>
            <dt>{localizedCopy.shipment}</dt>
            <dd className={styles.shipmentNumber}>{shipmentNumber}</dd>
          </div>
          <div>
            <dt>{locale === 'ru' ? 'Места' : 'Places'}</dt>
            <dd>{localizedCopy.place(placeCount)}</dd>
          </div>
        </dl>
        <div className={styles.actions}>
          <Button isDisabled={isResolved} onPress={() => resolve(onRetryAll)}>{localizedCopy.retryAll}</Button>
          <Button variant="secondary" isDisabled={isResolved} onPress={() => resolve(onSelectPlaces)}>{localizedCopy.selectPlaces}</Button>
          <Button variant="ghost" isDisabled={isResolved} onPress={() => resolve(onResolveWithoutReprint)}>{localizedCopy.resolveWithoutReprint}</Button>
        </div>
      </div>
    </Surface>
  </section>;
}
