import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  fullyParallel: false,
  forbidOnly: true,
  retries: 0,
  workers: 1,
  reporter: 'list',
  expect: {
    timeout: 5_000,
    toHaveScreenshot: {
      animations: 'disabled',
    },
  },
  use: {
    baseURL: 'http://127.0.0.1:6006',
    browserName: 'chromium',
    viewport: { width: 1280, height: 800 },
    deviceScaleFactor: 1,
    locale: 'ru-RU',
    timezoneId: 'Europe/Moscow',
    contextOptions: { reducedMotion: 'reduce' },
    hasTouch: true,
    actionTimeout: 10_000,
    navigationTimeout: 30_000,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  webServer: {
    command: 'corepack pnpm --filter @puntiro/storybook exec storybook dev -p 6006 --ci --no-open',
    url: 'http://127.0.0.1:6006',
    reuseExistingServer: false,
    timeout: 120_000,
    env: { CI: 'true' },
    stdout: 'pipe',
    stderr: 'pipe',
  },
});
