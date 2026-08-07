import type { ReactNode } from 'react';
import type { TranslationKey } from './copy';

export type Locale = 'ru' | 'en';

export type InteractionMode = 'touch' | 'standard';

export interface PuntiroProviderProps {
  children: ReactNode;
  locale?: Locale;
  mode?: InteractionMode;
}

export interface PuntiroContextValue {
  locale: Locale;
  mode: InteractionMode;
  t(key: TranslationKey): string;
}
