import type { StoryInteractionMode, StoryLocale } from './helpers';

export interface VisualCase {
  id: string;
  locale: StoryLocale;
  mode: StoryInteractionMode;
}

const touchStoryIds = [
  'components-button--primary',
  'components-numberinput--default',
  'kiosk-patterns-shipmenttaskcard--long-number',
  'kiosk-patterns-printerpicker--multiple-ready',
  'kiosk-patterns-printprogress--partial',
  'kiosk-patterns-unknownprintresult--default',
] as const;

const standardStoryIds = [
  'components-button--primary',
  'components-select--placeholder',
] as const;

const locales = ['ru', 'en'] as const;

export const visualCases: readonly VisualCase[] = [
  ...locales.flatMap((locale) => touchStoryIds.map((id) => ({ id, locale, mode: 'touch' as const }))),
  ...locales.flatMap((locale) => standardStoryIds.map((id) => ({ id, locale, mode: 'standard' as const }))),
];

export function visualSnapshotName(visualCase: VisualCase): string {
  return `${visualCase.id}-${visualCase.locale}-${visualCase.mode}.png`;
}
