import type { SurfaceProps } from './Surface.types';
import styles from './Surface.module.css';

export function Surface({ children, as: Tag = 'div', variant = 'plain', padding = 'comfortable' }: SurfaceProps) {
  const className = [styles.root, styles[variant], styles[padding]].filter(Boolean).join(' ');

  return <Tag className={className}>{children}</Tag>;
}
