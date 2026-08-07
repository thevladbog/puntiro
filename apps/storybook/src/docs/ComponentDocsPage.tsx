import { Controls, Primary, Stories, Title } from '@storybook/addon-docs/blocks';
import { createElement, type ComponentType } from 'react';
import { useGlobals } from 'storybook/preview-api';
import { DocsSection } from './DocsSection';
import { defineDocumentation } from './defineDocumentation';
import { MaturityBadge } from './MaturityBadge';
import {
  maturityManifest,
  resolveMaturityEntriesForDocs,
  summarizeMaturity,
  type MaturityManifestEntry,
} from './maturity-manifest';
import type { ComponentDocumentationOptions } from './types';
import './docs.css';

function DocumentationList({ entries }: { entries: string[] }) {
  return (
    <ul className="puntiro-docs__list">
      {entries.map((entry, index) => <li key={`${index}-${entry}`}>{entry}</li>)}
    </ul>
  );
}

interface ResolvedComponentDocumentationOptions extends ComponentDocumentationOptions {
  maturityEntries: readonly MaturityManifestEntry[];
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
    overviewBody: (total: number) => `В первой волне ${total} exports. Сводка ниже всегда рассчитывается из maturity manifest.`,
    levels: 'Уровни',
    reviewedCount: (reviewed: number, total: number) => `${reviewed} из ${total} выполнено`,
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
    overviewBody: (total: number) => `The first wave contains ${total} exports. The summary below is always derived from the maturity manifest.`,
    levels: 'Levels',
    reviewedCount: (reviewed: number, total: number) => `${reviewed} of ${total} reviewed`,
  },
} as const;

const maturityLevelLabels = {
  ru: { draft: 'Draft', beta: 'Beta', stable: 'Stable', deprecated: 'Deprecated' },
  en: { draft: 'Draft', beta: 'Beta', stable: 'Stable', deprecated: 'Deprecated' },
} as const;

function reviewLabel(reviewed: boolean, locale: 'ru' | 'en') {
  const copy = reviewCopy[locale];
  return reviewed ? copy.reviewed : copy.notReviewed;
}

function ManualGateStatus({ entries, locale }: { entries: readonly MaturityManifestEntry[]; locale: 'ru' | 'en' }) {
  const copy = reviewCopy[locale];

  return (
    <DocsSection title={copy.heading}>
      <p className="puntiro-docs__automated-note"><strong>{copy.automated}:</strong> {copy.automatedValue}</p>
      <div className="puntiro-docs__review-grid">
        {entries.map((entry) => (
          <section className="puntiro-docs__review-card" key={entry.name}>
            <header><h3>{entry.name}</h3><MaturityBadge maturity={entry.level} /></header>
            <dl className="puntiro-docs__review-status">
              <div><dt>{copy.since}</dt><dd>{entry.since}</dd></div>
              <div><dt>{copy.touch}</dt><dd>{reviewLabel(entry.manualTouchReviewed, locale)}</dd></div>
              <div><dt>{copy.screenReader}</dt><dd>{reviewLabel(entry.manualScreenReaderReviewed, locale)}</dd></div>
            </dl>
          </section>
        ))}
      </div>
    </DocsSection>
  );
}

export function MaturityOverview() {
  const [globals] = useGlobals();
  const locale = globals.locale === 'en' ? 'en' : 'ru';
  const copy = reviewCopy[locale];
  const summary = summarizeMaturity(maturityManifest);
  const levelSummary = (Object.entries(summary.levelCounts) as [keyof typeof summary.levelCounts, number][])
    .filter(([, count]) => count > 0)
    .map(([level, count]) => `${maturityLevelLabels[locale][level]}: ${count}`)
    .join(' · ');

  return (
    <section className="puntiro-docs__maturity-overview" aria-labelledby="puntiro-maturity-overview">
      <h2 id="puntiro-maturity-overview">{copy.overview}</h2>
      <p>{copy.overviewBody(summary.total)}</p>
      <dl className="puntiro-docs__maturity-summary">
        <div><dt>{copy.levels}</dt><dd>{levelSummary}</dd></div>
        <div><dt>{copy.touch}</dt><dd>{copy.reviewedCount(summary.manualTouchReviewed, summary.total)}</dd></div>
        <div><dt>{copy.screenReader}</dt><dd>{copy.reviewedCount(summary.manualScreenReaderReviewed, summary.total)}</dd></div>
      </dl>
      <p>{copy.automatedValue}</p>
    </section>
  );
}

export function ComponentDocsPage({ title, documentation, maturityEntries }: ResolvedComponentDocumentationOptions) {
  const [globals] = useGlobals();
  const locale = globals.locale === 'en' ? 'en' : 'ru';
  const copy = documentation[locale];
  const maturityLevels = [...new Set(maturityEntries.map((entry) => entry.level))];

  return (
    <article className="puntiro-docs">
      <header className="puntiro-docs__header">
        <Title>{title}</Title>
        <div className="puntiro-docs__maturity-levels">
          {maturityLevels.map((level) => <MaturityBadge key={level} maturity={level} />)}
        </div>
        <p className="puntiro-docs__overview">{copy.overview}</p>
      </header>
      <Primary />
      <DocsSection title="Anatomy"><DocumentationList entries={copy.anatomy} /></DocsSection>
      <DocsSection title="Variants"><DocumentationList entries={copy.variants} /></DocsSection>
      <DocsSection title="States"><DocumentationList entries={copy.states} /></DocsSection>
      <DocsSection title="Behavior"><DocumentationList entries={copy.behavior} /></DocsSection>
      <DocsSection title="Content"><DocumentationList entries={copy.content} /></DocsSection>
      <DocsSection title="Accessibility"><DocumentationList entries={copy.accessibility} /></DocsSection>
      <ManualGateStatus entries={maturityEntries} locale={locale} />
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
    documentation: defineDocumentation(options.documentation),
    maturityEntries: resolveMaturityEntriesForDocs(options.title),
  };

  return function DocumentationPage() {
    return createElement(ComponentDocsPage, pageOptions);
  };
}
