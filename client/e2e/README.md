# Basket E2E (Playwright) - Phase 2.3

## Purpose
Browser-based verification for basket guest/auth flows, merge, CSRF behavior, and UX. Tests are separated from production bundles.

## Setup
```bash
cd client
npm install
npm run e2e:install
```

## Environment / servers
- Default frontend: `http://localhost:4200` (`E2E_FRONTEND_URL` to override)
- Default API: `https://localhost:7199` (`E2E_API_URL` to override)
- Start API and Angular dev server before running tests (servers are not auto-started by config to avoid unsafe side effects).
- For Secure cookie behavior, run API over HTTPS and keep `ignoreHTTPSErrors` for localhost dev certs as configured.

## Safe test identities
- Do **not** use existing customer accounts or modify existing user records.
- Authenticated/merge browser scenarios require dedicated confirmed test accounts provisioned safely in a local Development/Test environment.
- Provide credentials via environment variables or local secure config (never commit secrets):
  - `E2E_TEST_USER_A_EMAIL`, `E2E_TEST_USER_A_PASSWORD`
  - `E2E_TEST_USER_B_EMAIL`, `E2E_TEST_USER_B_PASSWORD`
- If safe provisioning/confirmation is not possible, those tests are expected to be BLOCKED/skipped with documentation.

## Running
```bash
npm run e2e
npm run e2e:headed
npm run e2e:ui
```

## Artifacts & safety
- Screenshots/traces captured on failure (`playwright-report/`, `test-results/`).
- Do **not** attach or log raw cookies, JWTs, credentials, or secrets. Review artifacts before sharing.

## Structure
- `playwright.config.ts` - Playwright configuration
- `e2e/fixtures/` - reusable fixtures/helpers (guest/auth contexts)
- `e2e/tests/` - browser scenarios (guest, CSRF, auth/merge placeholders where blocked)
