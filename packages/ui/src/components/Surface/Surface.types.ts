import type { ReactNode } from 'react';

export interface SurfaceProps {
  children: ReactNode;
  as?: 'div' | 'section' | 'article';
  variant?: 'plain' | 'raised' | 'outlined';
  padding?: 'none' | 'compact' | 'comfortable';
}
