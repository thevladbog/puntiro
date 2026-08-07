export interface SelectOption {
  id: string;
  label: string;
  description?: string;
  isDisabled?: boolean;
}

export interface SelectProps {
  label: string;
  items: readonly SelectOption[];
  selectedId?: string;
  defaultSelectedId?: string;
  onSelectionChange?: (id: string) => void;
  placeholder?: string;
  description?: string;
  errorMessage?: string;
  isInvalid?: boolean;
  isDisabled?: boolean;
}
