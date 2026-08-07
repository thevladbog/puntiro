import type { ReactNode } from 'react';
import type { IconName } from '../../icons/iconRegistry';

export type ButtonVariant = 'primary' | 'secondary' | 'danger' | 'ghost';

export interface ButtonProps {
  children: ReactNode;
  variant?: ButtonVariant;
  isDisabled?: boolean;
  isLoading?: boolean;
  loadingLabel?: string;
  iconBefore?: IconName;
  onPress?: () => void;
  type?: 'button' | 'submit' | 'reset';
}

export interface IconButtonProps {
  label: string;
  icon: IconName;
  variant?: Exclude<ButtonVariant, 'primary'>;
  isDisabled?: boolean;
  onPress?: () => void;
}
