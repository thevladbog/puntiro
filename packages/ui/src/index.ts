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

export const puntiroUiVersion = '0.1.0' as const;
