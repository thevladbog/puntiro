import type { Maturity } from './types';

export interface MaturityManifestEntry {
  name: string;
  level: Maturity;
  since: string;
  docsStoryId: string;
  manualTouchReviewed: boolean;
  manualScreenReaderReviewed: boolean;
}

const beta = <Name extends string>(
  name: Name,
  docsStoryId: string,
): MaturityManifestEntry & { name: Name } => ({
  name,
  level: 'beta',
  since: '0.1.0',
  docsStoryId,
  manualTouchReviewed: false,
  manualScreenReaderReviewed: false,
});

export const maturityManifest = [
  beta('Button', 'components-button--default'),
  beta('ConnectivityBanner', 'kiosk-patterns-connectivitybanner--online'),
  beta('Dialog', 'components-dialog--confirmation'),
  beta('EmptyState', 'kiosk-patterns-system-states--no-jobs'),
  beta('ErrorState', 'kiosk-patterns-system-states--recoverable-error'),
  beta('IconButton', 'components-iconbutton--default'),
  beta('InlineMessage', 'components-inlinemessage--default'),
  beta('LoadingState', 'kiosk-patterns-system-states--loading'),
  beta('NumberInput', 'components-numberinput--default'),
  beta('PlaceCounter', 'kiosk-patterns-placecounter--touch-interaction'),
  beta('PrinterPicker', 'kiosk-patterns-printerpicker--touch-selection'),
  beta('PrintProgress', 'kiosk-patterns-printprogress--printing'),
  beta('ProgressIndicator', 'components-progressindicator--partial'),
  beta('PuntiroIcon', 'components-puntiroicon--default'),
  beta('Select', 'components-select--placeholder'),
  beta('ShipmentTaskCard', 'kiosk-patterns-shipmenttaskcard--ready'),
  beta('StatusBadge', 'components-statusbadge--default'),
  beta('Surface', 'components-surface--default'),
  beta('UnknownPrintResult', 'kiosk-patterns-unknownprintresult--default'),
] as const satisfies readonly MaturityManifestEntry[];

export type FirstWaveExportName = (typeof maturityManifest)[number]['name'];

export const maturityDocsBindings = {
  Button: ['Button'],
  ConnectivityBanner: ['ConnectivityBanner'],
  Dialog: ['Dialog'],
  IconButton: ['IconButton'],
  InlineMessage: ['InlineMessage'],
  NumberInput: ['NumberInput'],
  PlaceCounter: ['PlaceCounter'],
  PrinterPicker: ['PrinterPicker'],
  PrintProgress: ['PrintProgress'],
  ProgressIndicator: ['ProgressIndicator'],
  PuntiroIcon: ['PuntiroIcon'],
  Select: ['Select'],
  ShipmentTaskCard: ['ShipmentTaskCard'],
  StatusBadge: ['StatusBadge'],
  Surface: ['Surface'],
  'System states': ['EmptyState', 'LoadingState', 'ErrorState'],
  UnknownPrintResult: ['UnknownPrintResult'],
} as const satisfies Record<string, readonly FirstWaveExportName[]>;

export function resolveMaturityEntriesForDocs(title: string): readonly MaturityManifestEntry[] {
  const names = maturityDocsBindings[title as keyof typeof maturityDocsBindings];
  if (!names) {
    throw new Error(`No maturity manifest binding for docs page: ${title}`);
  }

  return names.map((name) => {
    const entry = maturityManifest.find((candidate) => candidate.name === name);
    if (!entry) {
      throw new Error(`Maturity manifest entry is missing: ${name}`);
    }
    return entry;
  });
}

export interface MaturitySummary {
  total: number;
  levelCounts: Record<Maturity, number>;
  manualTouchReviewed: number;
  manualScreenReaderReviewed: number;
}

export function summarizeMaturity(entries: readonly MaturityManifestEntry[]): MaturitySummary {
  const summary: MaturitySummary = {
    total: entries.length,
    levelCounts: { draft: 0, beta: 0, stable: 0, deprecated: 0 },
    manualTouchReviewed: 0,
    manualScreenReaderReviewed: 0,
  };

  for (const entry of entries) {
    summary.levelCounts[entry.level] += 1;
    summary.manualTouchReviewed += Number(entry.manualTouchReviewed);
    summary.manualScreenReaderReviewed += Number(entry.manualScreenReaderReviewed);
  }

  return summary;
}
