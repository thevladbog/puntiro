import { Button as AriaButton } from 'react-aria-components';
import { PuntiroIcon } from '../../icons/PuntiroIcon';
import { usePuntiro } from '../../provider/usePuntiro';
import type { ButtonProps } from './Button.types';
import styles from './Button.module.css';

export function Button({
  children,
  variant = 'primary',
  isDisabled = false,
  isLoading = false,
  loadingLabel,
  iconBefore,
  onPress,
  type = 'button'
}: ButtonProps) {
  const { locale } = usePuntiro();
  const isUnavailable = isDisabled || isLoading;
  const progressLabel = loadingLabel ?? (locale === 'ru' ? 'Выполняется' : 'In progress');
  const pressProps = !isUnavailable && onPress ? { onPress } : {};

  return <AriaButton
    type={type}
    isDisabled={isUnavailable}
    {...pressProps}
    className={`puntiro-control ${styles.root} ${styles[variant]}`}
    data-primary-action={variant === 'primary' ? 'true' : undefined}
  >
    {iconBefore ? <PuntiroIcon name={iconBefore} /> : null}
    <span className={styles.label}>{children}</span>
    {isLoading ? <span className={styles.progress} role="progressbar" aria-label={progressLabel} /> : null}
  </AriaButton>;
}
