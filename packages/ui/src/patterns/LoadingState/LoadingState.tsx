import { Surface } from '../../components/Surface/Surface';
import { PuntiroIcon } from '../../icons/PuntiroIcon';
import type { LoadingStateProps } from './LoadingState.types';
import styles from './LoadingState.module.css';

export function LoadingState({ label }: LoadingStateProps) {
  return <div className={styles.root} data-kiosk-working-region="true" role="status" aria-label={label}>
    <Surface variant="outlined" padding="comfortable">
      <div className={styles.layout}>
        <PuntiroIcon name="refresh" />
        <span>{label}</span>
      </div>
    </Surface>
  </div>;
}
