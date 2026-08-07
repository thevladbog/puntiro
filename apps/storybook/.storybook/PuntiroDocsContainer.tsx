import { DocsContainer, type DocsContainerProps } from '@storybook/addon-docs/blocks';
import { PuntiroProvider } from '@puntiro/ui';
import type { InteractionMode, Locale } from '@puntiro/ui';
import { useEffect, useState } from 'react';

const GLOBALS_UPDATED = 'globalsUpdated';

type PuntiroGlobals = {
  locale: Locale;
  interactionMode: InteractionMode;
};

type GlobalsUpdatedPayload = {
  globals?: Record<string, unknown>;
};

const defaultGlobals: PuntiroGlobals = {
  locale: 'ru',
  interactionMode: 'touch'
};

function normalizeGlobals(globals: Record<string, unknown>): PuntiroGlobals {
  return {
    locale: globals.locale === 'en' ? 'en' : 'ru',
    interactionMode: globals.interactionMode === 'standard' ? 'standard' : 'touch'
  };
}

function readLocationGlobals(): Record<string, string> {
  const serialized = new URLSearchParams(window.location.search).get('globals');
  if (!serialized) return {};

  return Object.fromEntries(serialized.split(';').flatMap((entry) => {
    const separator = entry.indexOf(':');
    return separator === -1 ? [] : [[entry.slice(0, separator), entry.slice(separator + 1)]];
  }));
}

export function PuntiroDocsContainer({ context, theme, children }: DocsContainerProps) {
  const [globals, setGlobals] = useState(() => normalizeGlobals({
    ...defaultGlobals,
    ...context.projectAnnotations.initialGlobals,
    ...readLocationGlobals()
  }));

  useEffect(() => {
    const handleGlobalsUpdated = ({ globals: nextGlobals }: GlobalsUpdatedPayload) => {
      if (nextGlobals) setGlobals(normalizeGlobals(nextGlobals));
    };

    context.channel.on(GLOBALS_UPDATED, handleGlobalsUpdated);
    return () => context.channel.off(GLOBALS_UPDATED, handleGlobalsUpdated);
  }, [context.channel]);

  return (
    <PuntiroProvider locale={globals.locale} mode={globals.interactionMode}>
      <DocsContainer context={context} theme={theme}>{children}</DocsContainer>
    </PuntiroProvider>
  );
}
