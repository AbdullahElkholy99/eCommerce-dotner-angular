import { test, expect, apiBaseURL, getAntiforgeryToken } from '../fixtures/test-fixtures';

/**
 * Authenticated isolation and guest->user merge browser scenarios.
 *
 * BLOCKED by default in this environment because safe, confirmed test accounts cannot be
 * provisioned/confirmed without modifying existing user records or relying on unavailable email flow.
 * Provide dedicated test credentials via env vars to enable (see e2e/README.md).
 */
const userAEmail = process.env.E2E_TEST_USER_A_EMAIL;
const userAPassword = process.env.E2E_TEST_USER_A_PASSWORD;
const userBEmail = process.env.E2E_TEST_USER_B_EMAIL;
const userBPassword = process.env.E2E_TEST_USER_B_PASSWORD;
const authConfigured = !!(userAEmail && userAPassword && userBEmail && userBPassword);

test.describe('Authenticated basket isolation & merge (browser)', () => {
  test.skip(!authConfigured, 'BLOCKED: dedicated confirmed test accounts not configured/provisioned safely');

  test('authenticated users have isolated baskets (separate browser contexts)', async ({ browser }) => {
    test.fail(true, 'Requires safe confirmed test accounts and running app/API; not executed in this verification run.');
  });

  test('guest basket merges into authenticated basket on login and is idempotent', async ({ browser }) => {
    test.fail(true, 'Requires safe confirmed test accounts and running app/API; not executed in this verification run.');
  });

  test('registration followed by merge', async ({ browser }) => {
    test.skip(true, 'BLOCKED: safe registration+email confirmation flow not available for automated browser verification');
  });
});
