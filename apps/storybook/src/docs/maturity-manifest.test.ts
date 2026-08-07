import { describe, expect, it } from 'vitest';
import {
  maturityDocsBindings,
  maturityManifest,
  resolveMaturityEntriesForDocs,
  summarizeMaturity,
  type MaturityManifestEntry,
} from './maturity-manifest';

const expectedEntries = [
  ['Button', 'components-button--default'],
  ['ConnectivityBanner', 'kiosk-patterns-connectivitybanner--online'],
  ['Dialog', 'components-dialog--confirmation'],
  ['EmptyState', 'kiosk-patterns-system-states--no-jobs'],
  ['ErrorState', 'kiosk-patterns-system-states--recoverable-error'],
  ['IconButton', 'components-iconbutton--default'],
  ['InlineMessage', 'components-inlinemessage--default'],
  ['LoadingState', 'kiosk-patterns-system-states--loading'],
  ['NumberInput', 'components-numberinput--default'],
  ['PlaceCounter', 'kiosk-patterns-placecounter--touch-interaction'],
  ['PrinterPicker', 'kiosk-patterns-printerpicker--touch-selection'],
  ['PrintProgress', 'kiosk-patterns-printprogress--printing'],
  ['ProgressIndicator', 'components-progressindicator--partial'],
  ['PuntiroIcon', 'components-puntiroicon--default'],
  ['Select', 'components-select--placeholder'],
  ['ShipmentTaskCard', 'kiosk-patterns-shipmenttaskcard--ready'],
  ['StatusBadge', 'components-statusbadge--default'],
  ['Surface', 'components-surface--default'],
  ['UnknownPrintResult', 'kiosk-patterns-unknownprintresult--default'],
] as const;

describe('maturityManifest', () => {
  it('lists every first-wave public export exactly once with its docs story', () => {
    expect(maturityManifest.map(({ name, docsStoryId }) => [name, docsStoryId]).sort()).toEqual(
      [...expectedEntries].sort(),
    );
  });

  it('keeps every first-wave export at Beta until both manual gates are complete', () => {
    expect(maturityManifest).toHaveLength(19);

    for (const entry of maturityManifest) {
      expect(entry).toMatchObject({
        level: 'beta',
        since: '0.1.0',
        manualTouchReviewed: false,
        manualScreenReaderReviewed: false,
      });
    }
  });

  it('binds every first-wave export once and resolves all grouped system-state gates', () => {
    const boundNames = Object.values(maturityDocsBindings).flat();

    expect([...boundNames].sort()).toEqual(maturityManifest.map((entry) => entry.name).sort());
    expect(new Set(boundNames).size).toBe(maturityManifest.length);
    expect(resolveMaturityEntriesForDocs('System states').map((entry) => entry.name)).toEqual([
      'EmptyState',
      'LoadingState',
      'ErrorState',
    ]);
  });

  it('derives level and manual-review counts from any manifest snapshot', () => {
    const entries: MaturityManifestEntry[] = [
      {
        name: 'BetaExample',
        level: 'beta',
        since: '0.1.0',
        docsStoryId: 'components-beta-example--default',
        manualTouchReviewed: false,
        manualScreenReaderReviewed: true,
      },
      {
        name: 'StableExample',
        level: 'stable',
        since: '0.2.0',
        docsStoryId: 'components-stable-example--default',
        manualTouchReviewed: true,
        manualScreenReaderReviewed: true,
      },
    ];

    expect(summarizeMaturity(entries)).toEqual({
      total: 2,
      levelCounts: { draft: 0, beta: 1, stable: 1, deprecated: 0 },
      manualTouchReviewed: 1,
      manualScreenReaderReviewed: 2,
    });
  });
});
