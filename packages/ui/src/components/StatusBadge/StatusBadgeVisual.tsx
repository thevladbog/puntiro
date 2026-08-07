import { PuntiroIcon } from '../../icons/PuntiroIcon';
import type { IconName } from '../../icons/iconRegistry';
import type { FeedbackTone } from './StatusBadge.types';
import styles from './StatusBadge.module.css';

const toneIcon: Record<FeedbackTone, IconName> = {
  neutral: 'minus',
  info: 'connection',
  success: 'check',
  warning: 'warning',
  danger: 'error'
};

interface StatusBadgeVisualProps {
  label: string;
  tone: FeedbackTone;
}

/** Internal visual content for components that must not nest a live region. */
export function StatusBadgeVisual({ label, tone }: StatusBadgeVisualProps) {
  return <span className={[styles.root, styles[tone]].filter(Boolean).join(' ')}>
    <PuntiroIcon name={toneIcon[tone]} />
    <span>{label}</span>
  </span>;
}
