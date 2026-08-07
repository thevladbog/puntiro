import type { Preview } from '@storybook/react-vite';
import { PuntiroProvider } from '@puntiro/ui';
import type { InteractionMode, Locale } from '@puntiro/ui';
import { docsTheme } from './theme';

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
