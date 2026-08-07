import { fileURLToPath } from 'node:url';
import { defineConfig, defineProject } from 'vitest/config';

export default defineConfig({
  test: {
    passWithNoTests: true,
    projects: [
      defineProject({
        resolve: {
          alias: {
            '@puntiro/ui': fileURLToPath(new URL('./packages/ui/src/index.ts', import.meta.url))
          }
        },
        test: {
          name: 'unit',
          environment: 'node',
          include: ['packages/**/*.test.{ts,tsx}', 'apps/storybook/src/**/*.test.{ts,tsx}']
        }
      }),
      'apps/storybook/vitest.config.ts'
    ]
  }
});
