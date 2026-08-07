import type { ReactElement, ReactNode } from 'react';
import type { ButtonVariant } from '../Button/Button.types';

export interface DialogAction {
  id: string;
  label: string;
  variant: ButtonVariant;
  onPress: (close: () => void) => void;
}

export interface DialogProps {
  trigger: ReactElement;
  title: string;
  description?: string;
  children: ReactNode;
  actions: readonly DialogAction[];
  isDismissible?: boolean;
}
