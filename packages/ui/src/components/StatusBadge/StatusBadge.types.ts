export type FeedbackTone = 'neutral' | 'info' | 'success' | 'warning' | 'danger';

export interface StatusBadgeProps {
  label: string;
  tone?: FeedbackTone;
  announce?: boolean;
}
