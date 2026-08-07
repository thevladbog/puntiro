import { createContext } from 'react';
import { I18nProvider } from 'react-aria-components';
import { copy } from './copy';
import type { PuntiroContextValue, PuntiroProviderProps } from './types';

export const PuntiroContext = createContext<PuntiroContextValue | null>(null);

export function PuntiroProvider({
  children,
  locale = 'ru',
  mode = 'touch'
}: PuntiroProviderProps) {
  const reactAriaLocale = locale === 'ru' ? 'ru-RU' : 'en-US';
  const value: PuntiroContextValue = {
    locale,
    mode,
    t: (key) => copy[locale][key]
  };

  return (
    <I18nProvider locale={reactAriaLocale}>
      <PuntiroContext.Provider value={value}>
        <div lang={locale} data-locale={locale} data-interaction-mode={mode}>
          {children}
        </div>
      </PuntiroContext.Provider>
    </I18nProvider>
  );
}
