import type { IconName } from './iconRegistry';
import { getIcon } from './iconRegistry';
import styles from './PuntiroIcon.module.css';

export interface PuntiroIconProps {
  name: IconName;
}

export function PuntiroIcon({ name }: PuntiroIconProps) {
  const Icon = getIcon(name);

  return <Icon className={styles.icon} aria-hidden="true" focusable="false" strokeWidth={2} />;
}
