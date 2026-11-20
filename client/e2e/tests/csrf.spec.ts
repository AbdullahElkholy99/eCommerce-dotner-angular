import { test, expect, apiBaseURL, getAntiforgeryToken } from '../fixtures/test-fixtures';

test.describe('CSRF protection (basket & logout API)', () => {
  test('safe GET basket does not require antiforgery token', async ({ request }) => {
    const res = await request.get(`${apiBaseURL}/api/Baskets`, { failOnStatusCode: false });
    // 200 expected (may issue guest context/cookie behavior varies), but must not be CSRF rejection
    expect(res.status()).not.toBe(400);
  });

  test('unsafe guest mutation without antiforgery token is rejected', async ({ request }) => {
    const res = await request.post(`${apiBaseURL}/api/Baskets/items`, {
      data: { productId: 1, quantity: 1 },
      failOnStatusCode: false,
    });
    expect(res.status()).toBe(400);
    const body = await res.json().catch(() => ({}));
    expect(String(body?.message ?? '').toLowerCase()).toContain('antiforgery');
  });

  test('unsafe guest mutation with invalid antiforgery token is rejected', async ({ request }) => {
    // Ensure antiforgery cookie context may exist for this client, but send invalid header token
    await getAntiforgeryToken(request);
    const res = await request.post(`${apiBaseURL}/api/Baskets/items`, {
      headers: { 'X-XSRF-TOKEN': 'invalid-token-value' },
      data: { productId: 1, quantity: 1 },
      failOnStatusCode: false,
    });
    expect(res.status()).toBe(400);
  });

  test('unsafe guest mutation with valid antiforgery token is accepted (or fails for non-CSRF reasons only)', async ({ request }) => {
    const token = await getAntiforgeryToken(request);
    expect(token).toBeTruthy();
    const res = await request.post(`${apiBaseURL}/api/Baskets/items`, {
      headers: { 'X-XSRF-TOKEN': token! },
      data: { productId: 1, quantity: 1 },
      failOnStatusCode: false,
    });
    expect([200, 400, 404, 409, 429]).toContain(res.status());
    if (res.status() === 400) {
      const body = await res.json().catch(() => ({}));
      expect(String(body?.message ?? '').toLowerCase()).not.toContain('antiforgery');
    }

    // Cleanup best-effort
    const token2 = await getAntiforgeryToken(request);
    if (token2) await request.delete(`${apiBaseURL}/api/Baskets`, { headers: { 'X-XSRF-TOKEN': token2 }, failOnStatusCode: false });
  });

  test('safe GET logout does not require antiforgery token but must not perform logout (405)', async ({ request, context }) => {
    // Include an auth cookie context to represent cookie-authenticated client; GET must remain non-mutating.
    await context.addCookies([{ name: 'token', value: 'dummy.invalid.token', domain: 'localhost', path: '/', httpOnly: true, secure: true, sameSite: 'None' }]);
    const res = await request.get(`${apiBaseURL}/api/Account/Logout`, { failOnStatusCode: false });
    expect(res.status()).toBe(405);
  });

  test('unsafe POST logout with cookie and missing antiforgery token is rejected', async ({ request, context }) => {
    await context.addCookies([{ name: 'token', value: 'dummy.invalid.token', domain: 'localhost', path: '/', httpOnly: true, secure: true, sameSite: 'None' }]);
    const res = await request.post(`${apiBaseURL}/api/Account/Logout`, { data: {}, failOnStatusCode: false });
    expect(res.status()).toBe(400);
    const body = await res.json().catch(() => ({}));
    expect(String(body?.message ?? '').toLowerCase()).toContain('antiforgery');
  });

  test('unsafe POST logout with cookie and invalid antiforgery token is rejected', async ({ request, context }) => {
    await context.addCookies([{ name: 'token', value: 'dummy.invalid.token', domain: 'localhost', path: '/', httpOnly: true, secure: true, sameSite: 'None' }]);
    await getAntiforgeryToken(request);
    const res = await request.post(`${apiBaseURL}/api/Account/Logout`, {
      headers: { 'X-XSRF-TOKEN': 'invalid-token-value' },
      data: {},
      failOnStatusCode: false,
    });
    expect(res.status()).toBe(400);
  });

  test('unsafe POST logout with cookie and valid antiforgery token succeeds', async ({ request, context }) => {
    await context.addCookies([{ name: 'token', value: 'dummy.invalid.token', domain: 'localhost', path: '/', httpOnly: true, secure: true, sameSite: 'None' }]);
    const token = await getAntiforgeryToken(request);
    expect(token).toBeTruthy();
    const res = await request.post(`${apiBaseURL}/api/Account/Logout`, {
      headers: { 'X-XSRF-TOKEN': token! },
      data: {},
      failOnStatusCode: false,
    });
    // Controller performs best-effort revocation/clear and returns 200 response contract
    expect([200, 429]).toContain(res.status());
    if (res.status() === 200) {
      const body = await res.json().catch(() => ({}));
      expect(body?.statusCode).toBe(200);
    }
  });
});
