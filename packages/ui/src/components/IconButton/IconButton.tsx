import { Button as AriaButton } from 'react-aria-components';
import { PuntiroIcon } from '../../icons/PuntiroIcon';
import type { IconButtonProps } from '../Button/Button.types';
import styles from './IconButton.module.css';

export function IconButton({
  label,
  icon,
  variant = 'secondary',
  isDisabled = false,
  onPress
}: IconButtonProps) {
  const pressProps = !isDisabled && onPress ? { onPress } : {};

  return <AriaButton
    aria-label={label}
    isDisabled={isDisabled}
    {...pressProps}
    className={`${styles.root} ${styles[variant]}`}
  >
    <PuntiroIcon name={icon} />
  </AriaButton>;
}
