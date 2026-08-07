export interface PlaceCounterProps {
  value: number;
  onChange: (value: number) => void;
  minValue?: number;
  maxValue?: number;
  isDisabled?: boolean;
}
