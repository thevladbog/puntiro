import { usePuntiro } from '@puntiro/ui';
import './start.css';

const labels = {
  ru: { foundations: 'Основы', components: 'Компоненты', map: 'Карта системы', brand: 'Бренд и токены', brandCopy: 'Утвержденные значения и роли.', behavior: 'Поведение', behaviorCopy: 'Доступные interaction contracts.', patterns: 'Patterns', patternsCopy: 'Доменные сборки без I/O.', rules: 'Правила', rulesCopy: 'Композиция, контент и доступность.', preview: 'Live preview', previewCopy: 'Компоненты появятся здесь после их реализации.' },
  en: { foundations: 'Foundations', components: 'Components', map: 'System map', brand: 'Brand and tokens', brandCopy: 'Approved values and roles.', behavior: 'Behavior', behaviorCopy: 'Accessible interaction contracts.', patterns: 'Patterns', patternsCopy: 'Domain assemblies without I/O.', rules: 'Rules', rulesCopy: 'Composition, content, and accessibility.', preview: 'Live preview', previewCopy: 'Components will appear here after implementation.' },
} as const;

export function StartLayout() {
  const { locale } = usePuntiro();
  const copy = labels[locale];

  return <>
    <nav className="puntiro-start__actions" aria-label={copy.map}>
      <a href="#foundations">{copy.foundations}</a><a href="#components">{copy.components}</a>
    </nav>
    <section className="puntiro-start__map" aria-labelledby="puntiro-system-map">
      <h2 id="puntiro-system-map">{copy.map}</h2>
      <div>{[[copy.brand, copy.brandCopy], [copy.behavior, copy.behaviorCopy], [copy.patterns, copy.patternsCopy], [copy.rules, copy.rulesCopy]].map(([title, body]) => <section key={title}><h3>{title}</h3><p>{body}</p></section>)}</div>
    </section>
    <aside className="puntiro-start__preview" aria-label={copy.preview}><strong>{copy.preview}</strong><span>{copy.previewCopy}</span></aside>
  </>;
}
