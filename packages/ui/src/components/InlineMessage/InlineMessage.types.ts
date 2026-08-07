import type { ReactNode } from 'react';
import type { FeedbackTone } from '../StatusBadge/StatusBadge.types';

export interface InlineMessageProps {
  title: string;
  children?: ReactNode;
  tone?: FeedbackTone;
}
