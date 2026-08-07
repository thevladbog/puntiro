import '@puntiro/tokens/tokens.css';
import './styles/global.css';
import './styles/utilities.css';

export { PuntiroProvider } from './provider/PuntiroProvider';
export { usePuntiro } from './provider/usePuntiro';
export type {
  InteractionMode,
  Locale,
  PuntiroContextValue,
  PuntiroProviderProps
} from './provider/types';
export type { TranslationKey } from './provider/copy';

export { PuntiroIcon } from './icons/PuntiroIcon';
export type { PuntiroIconProps } from './icons/PuntiroIcon';
export type { IconName } from './icons/iconRegistry';

export { Button } from './components/Button/Button';
export { IconButton } from './components/IconButton/IconButton';
export type { ButtonProps, ButtonVariant, IconButtonProps } from './components/Button/Button.types';

export { NumberInput } from './components/NumberInput/NumberInput';
export type { NumberInputProps } from './components/NumberInput/NumberInput.types';

export { Select } from './components/Select/Select';
export type { SelectOption, SelectProps } from './components/Select/Select.types';

export { Dialog } from './components/Dialog/Dialog';
export type { DialogAction, DialogProps } from './components/Dialog/Dialog.types';

export { Surface } from './components/Surface/Surface';
export type { SurfaceProps } from './components/Surface/Surface.types';
export { StatusBadge } from './components/StatusBadge/StatusBadge';
export type { FeedbackTone, StatusBadgeProps } from './components/StatusBadge/StatusBadge.types';
export { InlineMessage } from './components/InlineMessage/InlineMessage';
export type { InlineMessageProps } from './components/InlineMessage/InlineMessage.types';
export { ProgressIndicator } from './components/ProgressIndicator/ProgressIndicator';
export type { ProgressIndicatorProps } from './components/ProgressIndicator/ProgressIndicator.types';

export { ShipmentTaskCard } from './patterns/ShipmentTaskCard/ShipmentTaskCard';
export type { ShipmentTaskCardProps, ShipmentTaskStatus } from './patterns/ShipmentTaskCard/ShipmentTaskCard.types';
export { ConnectivityBanner } from './patterns/ConnectivityBanner/ConnectivityBanner';
export type { ConnectivityBannerProps, ConnectivityState } from './patterns/ConnectivityBanner/ConnectivityBanner.types';

export const puntiroUiVersion = '0.1.0' as const;
