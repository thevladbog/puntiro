import { describe, expect, it } from 'vitest';
import { maturityManifest } from './maturity-manifest';

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
});
