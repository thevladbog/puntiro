import type { ReactNode } from 'react';
import { usePuntiro } from '@puntiro/ui';
import './start.css';

const labels = {
  ru: { brandAction: 'Основы: бренд', tokensAction: 'Основы: токены', map: 'Карта системы', brand: 'Бренд и токены', brandCopy: 'Утвержденные значения и роли.', behavior: 'Поведение', behaviorCopy: 'Доступные контракты взаимодействия.', patterns: 'Паттерны', patternsCopy: 'Доменные сборки без ввода-вывода.', rules: 'Правила', rulesCopy: 'Композиция, контент и доступность.', preview: 'Предпросмотр' },
  en: { brandAction: 'Foundations: Brand', tokensAction: 'Foundations: Tokens', map: 'System map', brand: 'Brand and tokens', brandCopy: 'Approved values and roles.', behavior: 'Behavior', behaviorCopy: 'Accessible interaction contracts.', patterns: 'Patterns', patternsCopy: 'Domain assemblies without I/O.', rules: 'Rules', rulesCopy: 'Composition, content, and accessibility.', preview: 'Live preview' },
} as const;

export const START_ACTIONS = [
  { id: 'foundations-brand--docs', href: './?path=/docs/foundations-brand--docs', label: 'brandAction' },
  { id: 'foundations-tokens--docs', href: './?path=/docs/foundations-tokens--docs', label: 'tokensAction' },
] as const;

export function StartLayout({ preview }: { preview?: ReactNode }) {
  const { locale } = usePuntiro();
  const copy = labels[locale];

  return <>
    <nav className="puntiro-start__actions" aria-label={copy.map}>
      {START_ACTIONS.map((action) => <a key={action.id} href={action.href} target="_top">{copy[action.label]}</a>)}
    </nav>
    <section className="puntiro-start__map" aria-labelledby="puntiro-system-map">
      <h2 id="puntiro-system-map">{copy.map}</h2>
      <div>{[[copy.brand, copy.brandCopy], [copy.behavior, copy.behaviorCopy], [copy.patterns, copy.patternsCopy], [copy.rules, copy.rulesCopy]].map(([title, body]) => <section key={title}><h3>{title}</h3><p>{body}</p></section>)}</div>
    </section>
    {preview ? <aside className="puntiro-start__preview" aria-label={copy.preview}><strong>{copy.preview}</strong>{preview}</aside> : null}
  </>;
}
