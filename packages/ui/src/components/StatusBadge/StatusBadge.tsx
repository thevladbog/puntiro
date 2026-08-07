import { PuntiroIcon } from '../../icons/PuntiroIcon';
import type { IconName } from '../../icons/iconRegistry';
import type { FeedbackTone, StatusBadgeProps } from './StatusBadge.types';
import styles from './StatusBadge.module.css';

const toneIcon: Record<FeedbackTone, IconName> = {
  neutral: 'minus',
  info: 'connection',
  success: 'check',
  warning: 'warning',
  danger: 'error'
};

export function StatusBadge({ label, tone = 'neutral' }: StatusBadgeProps) {
  return <span className={[styles.root, styles[tone]].filter(Boolean).join(' ')} role="status">
    <PuntiroIcon name={toneIcon[tone]} />
    <span>{label}</span>
  </span>;
}
