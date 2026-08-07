import { usePuntiro } from '../../provider/usePuntiro';
import type { ProgressIndicatorProps } from './ProgressIndicator.types';
import styles from './ProgressIndicator.module.css';

function normalizeMax(max: number): number {
  return Number.isFinite(max) && max > 0 ? max : 1;
}

function normalizeValue(value: number, max: number): number {
  if (!Number.isFinite(value)) return 0;
  return Math.min(Math.max(value, 0), max);
}

export function ProgressIndicator({ value, max, label, counterText }: ProgressIndicatorProps) {
  const { locale } = usePuntiro();
  const normalizedMax = normalizeMax(max);
  const normalizedValue = normalizeValue(value, normalizedMax);
  const counter = counterText ?? (locale === 'ru'
    ? `${normalizedValue} из ${normalizedMax}`
    : `${normalizedValue} of ${normalizedMax}`);

  return <div className={styles.root}>
    <div className={styles.header}>
      <span>{label}</span>
      <span className={styles.counter}>{counter}</span>
    </div>
    <progress
      aria-label={label}
      aria-valuemax={normalizedMax}
      aria-valuemin={0}
      aria-valuenow={normalizedValue}
      className={styles.progress}
      max={normalizedMax}
      value={normalizedValue}
    />
  </div>;
}
