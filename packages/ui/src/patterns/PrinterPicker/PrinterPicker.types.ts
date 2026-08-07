export type PrinterLanguage = 'zpl' | 'tspl';

export type PrinterState = 'ready' | 'busy' | 'offline' | 'error';

export interface PrinterOption {
  id: string;
  name: string;
  language: PrinterLanguage;
  state: PrinterState;
}

export interface PrinterPickerProps {
  printers: readonly PrinterOption[];
  selectedId?: string;
  onSelectionChange: (id: string) => void;
}
