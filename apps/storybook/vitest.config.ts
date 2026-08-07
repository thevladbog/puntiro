import { defineProject } from 'vitest/config';
import { playwright } from '@vitest/browser-playwright';
import { storybookTest } from '@storybook/addon-vitest/vitest-plugin';

export default defineProject({
  plugins: [storybookTest({ configDir: 'apps/storybook/.storybook' })],
  test: {
    name: 'storybook',
    passWithNoTests: false,
    setupFiles: '.storybook/vitest.setup.ts',
    browser: {
      enabled: true,
      headless: true,
      provider: playwright({}),
      instances: [{ browser: 'chromium' }]
    }
  }
});
