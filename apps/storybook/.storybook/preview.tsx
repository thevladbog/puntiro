import type { Preview } from '@storybook/react-vite';
import { PuntiroProvider } from '@puntiro/ui';
import type { InteractionMode, Locale } from '@puntiro/ui';

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
  decorators: [
    (Story, context) => (
      <PuntiroProvider
        locale={context.globals.locale as Locale}
        mode={context.globals.interactionMode as InteractionMode}
      >
        <Story />
      </PuntiroProvider>
    )
  ]
} satisfies Preview;

export default preview;
