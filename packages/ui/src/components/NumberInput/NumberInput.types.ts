export interface NumberInputProps {
  label: string;
  value?: number;
  defaultValue?: number;
  onChange?: (value: number) => void;
  minValue?: number;
  maxValue?: number;
  step?: number;
  description?: string;
  errorMessage?: string;
  isInvalid?: boolean;
  isDisabled?: boolean;
}
