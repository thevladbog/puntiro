import type { StatusBadgeProps } from './StatusBadge.types';
import { StatusBadgeVisual } from './StatusBadgeVisual';

export function StatusBadge({ label, tone = 'neutral' }: StatusBadgeProps) {
  return <span role="status"><StatusBadgeVisual label={label} tone={tone} /></span>;
}
