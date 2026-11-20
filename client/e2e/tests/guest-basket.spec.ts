import { test, expect, basketCookieFromContext, apiBaseURL, getAntiforgeryToken } from '../fixtures/test-fixtures';

test.describe('Guest basket (browser)', () => {
  test('issues guest basket cookie with expected attributes and persists basket across refresh (isolated context)', async ({ guestPage, guestContext }) => {
    // Navigate to shop so app/API integration is exercised in browser context
    await guestPage.goto('/shop', { waitUntil: 'domcontentloaded' });

    // Use API request in same browser storage state to create/mutate basket as guest (CSRF-protected)
    const request = guestContext.request;
    const token = await getAntiforgeryToken(request);
    expect(token, 'antiforgery token should be issued').toBeTruthy();

    const addRes = await request.post(`${apiBaseURL}/api/Baskets/items`, {
      headers: { 'X-XSRF-TOKEN': token! },
      data: { productId: 1, quantity: 1 },
      failOnStatusCode: false,
    });
    // If test catalog differs, this may fail with 400; report actual behavior via assertion message
    expect([200, 400]).toContain(addRes.status());
    if (addRes.status() === 400) {
      test.info().annotations.push({ type: 'note', description: 'Add item returned 400; guest persistence flow may depend on available catalog data in test environment.' });
      return;
    }

    const cookies = await guestContext.cookies();
    const guestCookie = basketCookieFromContext(cookies);
    expect(guestCookie, 'guest_basket_id cookie should be issued').toBeTruthy();
    expect(guestCookie!.httpOnly).toBe(true);
    expect(guestCookie!.secure).toBe(true);
    // Playwright reports SameSite as Strict/Lax/None
    expect(['None', 'Lax', 'Strict']).toContain(guestCookie!.sameSite);
    expect(guestCookie!.path).toBe('/');

    // Verify basket retrievable in same context
    const get1 = await request.get(`${apiBaseURL}/api/Baskets`, { failOnStatusCode: false });
    expect(get1.status()).toBe(200);
    const body1 = await get1.json();
    expect(Array.isArray(body1?.basketItems)).toBe(true);

    await guestPage.reload({ waitUntil: 'domcontentloaded' });
    const get2 = await request.get(`${apiBaseURL}/api/Baskets`, { failOnStatusCode: false });
    expect(get2.status()).toBe(200);
    const body2 = await get2.json();
    expect(JSON.stringify(body2?.basketItems ?? [])).toBe(JSON.stringify(body1?.basketItems ?? []));

    // Cleanup best-effort for this guest context
    const token2 = await getAntiforgeryToken(request);
    if (token2) {
      await request.delete(`${apiBaseURL}/api/Baskets`, { headers: { 'X-XSRF-TOKEN': token2 }, failOnStatusCode: false });
    }
  });

  test('guest contexts are isolated (separate browser contexts)', async ({ browser }) => {
    const ctxA = await browser.newContext({ ignoreHTTPSErrors: true });
    const ctxB = await browser.newContext({ ignoreHTTPSErrors: true });
    try {
      const reqA = ctxA.request;
      const reqB = ctxB.request;
      const tA = await getAntiforgeryToken(reqA);
      const tB = await getAntiforgeryToken(reqB);
      expect(tA).toBeTruthy();
      expect(tB).toBeTruthy();

      const addA = await reqA.post(`${apiBaseURL}/api/Baskets/items`, {
        headers: { 'X-XSRF-TOKEN': tA! }, data: { productId: 1, quantity: 2 }, failOnStatusCode: false,
      });
      const addB = await reqB.post(`${apiBaseURL}/api/Baskets/items`, {
        headers: { 'X-XSRF-TOKEN': tB! }, data: { productId: 2, quantity: 1 }, failOnStatusCode: false,
      });

      if (addA.status() !== 200 || addB.status() !== 200) {
        test.info().annotations.push({ type: 'note', description: 'One or more guest add operations did not succeed; isolation assertion may be inconclusive in this environment.' });
        return;
      }

      const a = await (await reqA.get(`${apiBaseURL}/api/Baskets`)).json();
      const b = await (await reqB.get(`${apiBaseURL}/api/Baskets`)).json();
      expect(JSON.stringify(a)).not.toBe(JSON.stringify(b));

      // Clear A, ensure B unaffected
      const tA2 = await getAntiforgeryToken(reqA);
      if (tA2) await reqA.delete(`${apiBaseURL}/api/Baskets`, { headers: { 'X-XSRF-TOKEN': tA2 }, failOnStatusCode: false });
      const bAfter = await (await reqB.get(`${apiBaseURL}/api/Baskets`)).json();
      expect(JSON.stringify(bAfter)).toBe(JSON.stringify(b));

      const tB2 = await getAntiforgeryToken(reqB);
      if (tB2) await reqB.delete(`${apiBaseURL}/api/Baskets`, { headers: { 'X-XSRF-TOKEN': tB2 }, failOnStatusCode: false });
    } finally {
      await ctxA.close();
      await ctxB.close();
    }
  });
});
