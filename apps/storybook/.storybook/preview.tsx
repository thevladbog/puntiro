import type { Preview } from '@storybook/react-vite';
import { PuntiroProvider } from '@puntiro/ui';
import type { InteractionMode, Locale } from '@puntiro/ui';
import { addons } from 'storybook/preview-api';
import { PuntiroDocsContainer } from './PuntiroDocsContainer';
import { docsTheme } from './theme';

const storyLoadRecoveryKey = 'puntiro:last-story-load-recovery';

addons.getChannel().on('storyMissing', (storyId) => {
  if (typeof storyId !== 'string') return;

  const lastRecovery = Number(window.sessionStorage.getItem(storyLoadRecoveryKey) ?? 0);
  if (Date.now() - lastRecovery < 10_000) return;

  window.sessionStorage.setItem(storyLoadRecoveryKey, String(Date.now()));
  window.location.reload();
});

type PuntiroStoryParameters = {
  puntiro?: {
    reducedMotion?: boolean;
  };
};

const preview = {
  globalTypes: {
    locale: {
      description: 'Locale for Puntiro content',
      toolbar: {
        items: [
          { value: 'ru', title: 'RU' },
          { value: 'en', title: 'EN' }
        ]
      }
    },
    interactionMode: {
      description: 'Interaction target size',
      toolbar: {
        items: [
          { value: 'touch', title: 'Touch' },
          { value: 'standard', title: 'Standard' }
        ]
      }
    }
  },
  initialGlobals: {
    locale: 'ru',
    interactionMode: 'touch'
  },
  parameters: {
    a11y: {
      test: 'error'
    },
    docs: {
      container: PuntiroDocsContainer,
      theme: docsTheme
    }
  },
  decorators: [
    (Story, context) => {
      const parameters = context.parameters as PuntiroStoryParameters;
      const reducedMotion = parameters.puntiro?.reducedMotion === true;
      const environmentProps = reducedMotion ? { 'data-puntiro-reduced-motion': 'true' } : {};

      return <PuntiroProvider
        locale={context.globals.locale as Locale}
        mode={context.globals.interactionMode as InteractionMode}
      >
        <div {...environmentProps}><Story /></div>
      </PuntiroProvider>;
    }
  ]
} satisfies Preview;

export default preview;
