export interface UnknownPrintResultProps {
  shipmentNumber: string;
  placeCount: number;
  onRetryAll: () => void;
  onSelectPlaces: () => void;
  onResolveWithoutReprint: () => void;
}
