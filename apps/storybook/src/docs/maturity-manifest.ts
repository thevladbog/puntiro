import type { Maturity } from './types';

export interface MaturityManifestEntry {
  name: string;
  level: Maturity;
  since: string;
  docsStoryId: string;
  manualTouchReviewed: boolean;
  manualScreenReaderReviewed: boolean;
}

const beta = (
  name: string,
  docsStoryId: string,
): MaturityManifestEntry => ({
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

export function findMaturityEntry(name: string): MaturityManifestEntry | undefined {
  return maturityManifest.find((entry) => entry.name === name);
}
