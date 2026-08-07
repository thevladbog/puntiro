export interface ErrorStateProps {
  title: string;
  description: string;
  recoveryAction?: { label: string; onPress: () => void };
}
