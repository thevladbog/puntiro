import type { Maturity } from './types';

const maturityLabels: Record<Maturity, string> = {
  draft: 'Draft',
  beta: 'Beta',
  stable: 'Stable',
  deprecated: 'Deprecated'
};

interface MaturityBadgeProps {
  maturity: Maturity;
}

export function MaturityBadge({ maturity }: MaturityBadgeProps) {
  return <span className={`puntiro-docs__maturity puntiro-docs__maturity--${maturity}`}>{maturityLabels[maturity]}</span>;
}
