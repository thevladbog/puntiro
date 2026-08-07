import type { ReactNode } from 'react';

interface DocsSectionProps {
  title: string;
  children: ReactNode;
}

export function DocsSection({ title, children }: DocsSectionProps) {
  return (
    <section className="puntiro-docs__section">
      <h2>{title}</h2>
      {children}
    </section>
  );
}
