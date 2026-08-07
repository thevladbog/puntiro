import { Controls, Primary, Stories, Title } from '@storybook/addon-docs/blocks';
import { createElement, type ComponentType } from 'react';
import { useGlobals } from 'storybook/preview-api';
import { DocsSection } from './DocsSection';
import { defineDocumentation } from './defineDocumentation';
import { MaturityBadge } from './MaturityBadge';
import type { ComponentDocumentationOptions } from './types';
import './docs.css';

function DocumentationList({ entries }: { entries: string[] }) {
  return (
    <ul className="puntiro-docs__list">
      {entries.map((entry, index) => <li key={`${index}-${entry}`}>{entry}</li>)}
    </ul>
  );
}

export function ComponentDocsPage({ title, maturity, documentation }: ComponentDocumentationOptions) {
  const [globals] = useGlobals();
  const locale = globals.locale === 'en' ? 'en' : 'ru';
  const copy = documentation[locale];

  return (
    <article className="puntiro-docs">
      <header className="puntiro-docs__header">
        <Title>{title}</Title>
        <MaturityBadge maturity={maturity} />
        <p className="puntiro-docs__overview">{copy.overview}</p>
      </header>
      <Primary />
      <DocsSection title="Anatomy"><DocumentationList entries={copy.anatomy} /></DocsSection>
      <DocsSection title="Variants"><DocumentationList entries={copy.variants} /></DocsSection>
      <DocsSection title="States"><DocumentationList entries={copy.states} /></DocsSection>
      <DocsSection title="Behavior"><DocumentationList entries={copy.behavior} /></DocsSection>
      <DocsSection title="Content"><DocumentationList entries={copy.content} /></DocsSection>
      <DocsSection title="Accessibility"><DocumentationList entries={copy.accessibility} /></DocsSection>
      <DocsSection title="Usage"><DocumentationList entries={copy.usage} /></DocsSection>
      <DocsSection title="Do / Don't">
        <div className="puntiro-docs__guidance">
          <div><h3>Do</h3><DocumentationList entries={copy.do} /></div>
          <div><h3>Don't</h3><DocumentationList entries={copy.dont} /></div>
        </div>
      </DocsSection>
      <DocsSection title="Controls"><Controls /></DocsSection>
      <DocsSection title="Stories"><Stories title="" /></DocsSection>
      <DocsSection title="Changelog"><DocumentationList entries={copy.changelog} /></DocsSection>
    </article>
  );
}

export function createDocsPage(options: ComponentDocumentationOptions): ComponentType {
  const pageOptions = {
    ...options,
    documentation: defineDocumentation(options.documentation)
  };

  return function DocumentationPage() {
    return createElement(ComponentDocsPage, pageOptions);
  };
}
