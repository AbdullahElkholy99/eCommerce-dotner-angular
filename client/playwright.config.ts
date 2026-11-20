import { defineConfig, devices } from '@playwright/test';

/**
 * Phase 2.3 E2E configuration (basket/security verification).
 * - Uses isolated browser contexts per test.
 * - Captures screenshots/traces on failure; reports must never include cookies/tokens/secrets.
 * - Requires local API and Angular dev servers to be running (or started separately).
 */
const frontendBaseURL = process.env.E2E_FRONTEND_URL ?? 'http://localhost:4200';
const apiBaseURL = process.env.E2E_API_URL ?? 'https://localhost:7199';

export default defineConfig({
  testDir: './e2e/tests',
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: process.env.CI ? 1 : 1,
  timeout: 60_000,
  expect: { timeout: 10_000 },
  reporter: [
    ['list'],
    ['html', { open: 'never', outputFolder: 'playwright-report' }],
  ],
  use: {
    baseURL: frontendBaseURL,
    ignoreHTTPSErrors: true,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'off',
    actionTimeout: 15_000,
    navigationTimeout: 30_000,
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  // Servers are expected to be managed explicitly for this phase to avoid unsafe side effects.
  // Set E2E_FRONTEND_URL / E2E_API_URL to override defaults.
  metadata: {
    apiBaseURL,
    frontendBaseURL,
  },
});
