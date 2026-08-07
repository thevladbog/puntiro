export type ShipmentTaskStatus = 'ready' | 'updated' | 'attention';

export interface ShipmentTaskCardProps {
  shipmentNumber: string;
  receivedAt: Date;
  plannedShipAt?: Date;
  status: ShipmentTaskStatus;
  onOpen: () => void;
  isSelected?: boolean;
}
