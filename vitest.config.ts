import { defineConfig, defineProject } from 'vitest/config';

export default defineConfig({
  test: {
    passWithNoTests: true,
    projects: [
      defineProject({
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
