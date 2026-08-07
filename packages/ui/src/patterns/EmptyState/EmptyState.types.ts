export interface EmptyStateProps {
  title: string;
  description: string;
  action?: { label: string; onPress: () => void };
}
