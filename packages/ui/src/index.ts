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

export const puntiroUiVersion = '0.1.0' as const;
