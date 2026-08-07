import { useId } from 'react';
import type { FeedbackTone } from '../../components/StatusBadge/StatusBadge.types';
import { StatusBadgeVisual } from '../../components/StatusBadge/StatusBadgeVisual';
import { Surface } from '../../components/Surface/Surface';
import { usePuntiro } from '../../provider/usePuntiro';
import type { Locale } from '../../provider/types';
import type { PrinterOption, PrinterPickerProps, PrinterState } from './PrinterPicker.types';
import styles from './PrinterPicker.module.css';

const classes = {
  root: styles.root!,
  label: styles.label!,
  empty: styles.empty!,
  options: styles.options!,
  option: styles.option!,
  input: styles.input!,
  radioMark: styles.radioMark!,
  identity: styles.identity!,
  name: styles.name!,
  language: styles.language!
};

type PrinterPickerCopy = {
  label: string;
  empty: string;
  statuses: Record<PrinterState, string>;
};

const copy: Record<Locale, PrinterPickerCopy> = {
  ru: {
    label: 'Выберите принтер',
    empty: 'Принтеры не найдены',
    statuses: { ready: 'Готов', busy: 'Занят', offline: 'Не в сети', error: 'Ошибка' }
  },
  en: {
    label: 'Select a printer',
    empty: 'No printers found',
    statuses: { ready: 'Ready', busy: 'Busy', offline: 'Offline', error: 'Error' }
  }
};

const stateTone: Record<PrinterState, FeedbackTone> = {
  ready: 'success',
  busy: 'warning',
  offline: 'neutral',
  error: 'danger'
};

function accessibleOptionName(printer: PrinterOption): string {
  return `${printer.name} — ${printer.language.toUpperCase()}`;
}

function PrinterOptionRow({
  printer,
  statusLabel,
  groupName,
  isSelected,
  onSelectionChange
}: {
  printer: PrinterOption;
  statusLabel: string;
  groupName: string;
  isSelected: boolean;
  onSelectionChange: (id: string) => void;
}) {
  const statusId = useId();
  const isDisabled = printer.state !== 'ready';

  return <label
    className={classes.option}
    data-disabled={isDisabled || undefined}
    data-selected={isSelected || undefined}
  >
    <input
      type="radio"
      className={classes.input}
      name={groupName}
      value={printer.id}
      checked={isSelected}
      disabled={isDisabled}
      aria-label={accessibleOptionName(printer)}
      aria-describedby={statusId}
      onChange={() => onSelectionChange(printer.id)}
    />
    <span className={classes.radioMark} aria-hidden="true" />
    <span className={classes.identity}>
      <span className={classes.name}>{printer.name}</span>
      <span className={classes.language}>{printer.language.toUpperCase()}</span>
    </span>
    <span id={statusId}><StatusBadgeVisual label={statusLabel} tone={stateTone[printer.state]} /></span>
  </label>;
}

export function PrinterPicker({ printers, selectedId, onSelectionChange }: PrinterPickerProps) {
  const { locale } = usePuntiro();
  const localizedCopy = copy[locale];
  const groupId = useId();
  const labelId = useId();

  return <div
    id={groupId}
    role="radiogroup"
    aria-labelledby={labelId}
    className={classes.root}
    data-kiosk-working-region="true"
  >
    <span id={labelId} className={classes.label}>{localizedCopy.label}</span>
    {printers.length === 0
      ? <Surface variant="outlined" padding="comfortable"><p className={classes.empty}>{localizedCopy.empty}</p></Surface>
      : <div className={classes.options}>
        {printers.map((printer) => <PrinterOptionRow
          key={printer.id}
          printer={printer}
          statusLabel={localizedCopy.statuses[printer.state]}
          groupName={groupId}
          isSelected={selectedId === printer.id}
          onSelectionChange={onSelectionChange}
        />)}
      </div>}
  </div>;
}
