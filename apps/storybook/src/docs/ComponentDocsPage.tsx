import { Controls, Primary, Stories, Title } from '@storybook/addon-docs/blocks';
import { createElement, type ComponentType } from 'react';
import { useGlobals } from 'storybook/preview-api';
import { DocsSection } from './DocsSection';
import { defineDocumentation } from './defineDocumentation';
import { MaturityBadge } from './MaturityBadge';
import { findMaturityEntry, maturityManifest, type MaturityManifestEntry } from './maturity-manifest';
import type { ComponentDocumentationOptions } from './types';
import './docs.css';

function DocumentationList({ entries }: { entries: string[] }) {
  return (
    <ul className="puntiro-docs__list">
      {entries.map((entry, index) => <li key={`${index}-${entry}`}>{entry}</li>)}
    </ul>
  );
}

const reviewCopy = {
  ru: {
    heading: 'Статус проверки',
    since: 'В системе с версии',
    automated: 'Автоматические проверки',
    automatedValue: 'Выполняются отдельно командой pnpm check и не считаются ручным одобрением.',
    touch: 'Ручная проверка touch и перчаток',
    screenReader: 'Ручная проверка screen reader',
    reviewed: 'Выполнена',
    notReviewed: 'Не выполнена',
    overview: 'Зрелость первой волны',
    overviewBody: 'Все 19 exports имеют уровень Beta. Автоматические проверки не заменяют ручные проверки на Windows и оборудовании.',
  },
  en: {
    heading: 'Review status',
    since: 'In the system since',
    automated: 'Automated checks',
    automatedValue: 'Run separately with pnpm check and do not count as manual approval.',
    touch: 'Manual touch and glove review',
    screenReader: 'Manual screen-reader review',
    reviewed: 'Reviewed',
    notReviewed: 'Not reviewed',
    overview: 'First-wave maturity',
    overviewBody: 'All 19 exports are Beta. Automated checks do not replace manual review on Windows and physical hardware.',
  },
} as const;

function reviewLabel(reviewed: boolean, locale: 'ru' | 'en') {
  const copy = reviewCopy[locale];
  return reviewed ? copy.reviewed : copy.notReviewed;
}

function ManualGateStatus({ entry, locale }: { entry: MaturityManifestEntry; locale: 'ru' | 'en' }) {
  const copy = reviewCopy[locale];

  return (
    <DocsSection title={copy.heading}>
      <dl className="puntiro-docs__review-status">
        <div><dt>{copy.since}</dt><dd>{entry.since}</dd></div>
        <div><dt>{copy.automated}</dt><dd>{copy.automatedValue}</dd></div>
        <div><dt>{copy.touch}</dt><dd>{reviewLabel(entry.manualTouchReviewed, locale)}</dd></div>
        <div><dt>{copy.screenReader}</dt><dd>{reviewLabel(entry.manualScreenReaderReviewed, locale)}</dd></div>
      </dl>
    </DocsSection>
  );
}

export function MaturityOverview() {
  const [globals] = useGlobals();
  const locale = globals.locale === 'en' ? 'en' : 'ru';
  const copy = reviewCopy[locale];

  return (
    <section className="puntiro-docs__maturity-overview" aria-labelledby="puntiro-maturity-overview">
      <h2 id="puntiro-maturity-overview">{copy.overview}</h2>
      <p>{copy.overviewBody}</p>
      <p><strong>Beta</strong> · {maturityManifest.length} exports · {copy.touch}: {copy.notReviewed} · {copy.screenReader}: {copy.notReviewed}</p>
    </section>
  );
}

export function ComponentDocsPage({ title, maturity, documentation }: ComponentDocumentationOptions) {
  const [globals] = useGlobals();
  const locale = globals.locale === 'en' ? 'en' : 'ru';
  const copy = documentation[locale];
  const maturityEntry = findMaturityEntry(title);

  return (
    <article className="puntiro-docs">
      <header className="puntiro-docs__header">
        <Title>{title}</Title>
        <MaturityBadge maturity={maturityEntry?.level ?? maturity} />
        <p className="puntiro-docs__overview">{copy.overview}</p>
      </header>
      <Primary />
      <DocsSection title="Anatomy"><DocumentationList entries={copy.anatomy} /></DocsSection>
      <DocsSection title="Variants"><DocumentationList entries={copy.variants} /></DocsSection>
      <DocsSection title="States"><DocumentationList entries={copy.states} /></DocsSection>
      <DocsSection title="Behavior"><DocumentationList entries={copy.behavior} /></DocsSection>
      <DocsSection title="Content"><DocumentationList entries={copy.content} /></DocsSection>
      <DocsSection title="Accessibility"><DocumentationList entries={copy.accessibility} /></DocsSection>
      {maturityEntry ? <ManualGateStatus entry={maturityEntry} locale={locale} /> : null}
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
