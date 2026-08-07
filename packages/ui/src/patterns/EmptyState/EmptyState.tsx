import { Button } from '../../components/Button/Button';
import { Surface } from '../../components/Surface/Surface';
import { PuntiroIcon } from '../../icons/PuntiroIcon';
import type { EmptyStateProps } from './EmptyState.types';
import styles from './EmptyState.module.css';

export function EmptyState({ title, description, action }: EmptyStateProps) {
  return <div className={styles.root} data-kiosk-working-region="true">
    <Surface variant="outlined" padding="comfortable">
      <div className={styles.layout}>
        <PuntiroIcon name="archive" />
        <div className={styles.copy}>
          <h2>{title}</h2>
          <p>{description}</p>
        </div>
        {action ? <Button onPress={action.onPress}>{action.label}</Button> : null}
      </div>
    </Surface>
  </div>;
}
