import { Button } from '../../components/Button/Button';
import { InlineMessage } from '../../components/InlineMessage/InlineMessage';
import type { ErrorStateProps } from './ErrorState.types';
import styles from './ErrorState.module.css';

export function ErrorState({ title, description, recoveryAction }: ErrorStateProps) {
  return <div className={styles.root} data-kiosk-working-region="true">
    <InlineMessage title={title} tone="danger">{description}</InlineMessage>
    {recoveryAction
      ? <div className={styles.actions}><Button onPress={recoveryAction.onPress}>{recoveryAction.label}</Button></div>
      : null}
  </div>;
}
