import { PuntiroIcon } from '../../icons/PuntiroIcon';
import type { IconName } from '../../icons/iconRegistry';
import type { FeedbackTone } from '../StatusBadge/StatusBadge.types';
import type { InlineMessageProps } from './InlineMessage.types';
import styles from './InlineMessage.module.css';

const toneIcon: Record<FeedbackTone, IconName> = {
  neutral: 'minus',
  info: 'connection',
  success: 'check',
  warning: 'warning',
  danger: 'error'
};

export function InlineMessage({ title, children, tone = 'neutral' }: InlineMessageProps) {
  const role = tone === 'danger' ? 'alert' : 'status';

  return <div className={[styles.root, styles[tone]].filter(Boolean).join(' ')} role={role}>
    <PuntiroIcon name={toneIcon[tone]} />
    <div className={styles.content}>
      <strong>{title}</strong>
      {children ? <div className={styles.description}>{children}</div> : null}
    </div>
  </div>;
}
