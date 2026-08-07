import { NumberInput } from '../../components/NumberInput/NumberInput';
import { Surface } from '../../components/Surface/Surface';
import { usePuntiro } from '../../provider/usePuntiro';
import type { PlaceCounterProps } from './PlaceCounter.types';
import styles from './PlaceCounter.module.css';

const copy = {
  ru: { label: 'Количество мест', separator: 'из' },
  en: { label: 'Number of places', separator: 'of' }
} as const;

export function PlaceCounter({
  value,
  onChange,
  minValue = 1,
  maxValue = 100,
  isDisabled = false
}: PlaceCounterProps) {
  const { locale } = usePuntiro();
  const localizedCopy = copy[locale];
  const numberFormatter = new Intl.NumberFormat(locale === 'ru' ? 'ru-RU' : 'en-US');

  return <div className={styles.root} data-kiosk-working-region="true">
    <Surface variant="outlined" padding="comfortable">
      <div className={styles.layout}>
        <output className={styles.counter} aria-live="polite">
          {numberFormatter.format(value)} {localizedCopy.separator} {numberFormatter.format(maxValue)}
        </output>
        <NumberInput
          label={localizedCopy.label}
          value={value}
          onChange={onChange}
          minValue={minValue}
          maxValue={maxValue}
          isDisabled={isDisabled}
        />
      </div>
    </Surface>
  </div>;
}
