import { test as base, expect, APIRequestContext, BrowserContext, Page } from '@playwright/test';

export type TestFixtures = {
  guestPage: Page;
  guestContext: BrowserContext;
};

export const test = base.extend<TestFixtures>({
  guestContext: async ({ browser }, use) => {
    const context = await browser.newContext({
      ignoreHTTPSErrors: true,
    });
    await use(context);
    await context.close();
  },
  guestPage: async ({ guestContext }, use) => {
    const page = await guestContext.newPage();
    await use(page);
  },
});

export { expect };

export const apiBaseURL = process.env.E2E_API_URL ?? 'https://localhost:7199';

export async function getAntiforgeryToken(request: APIRequestContext): Promise<string | null> {
  const res = await request.get(`${apiBaseURL}/api/Antiforgery/token`, { failOnStatusCode: false });
  if (!res.ok()) return null;
  const data = await res.json().catch(() => null) as any;
  return typeof data?.token === 'string' ? data.token : null;
}

export function basketCookieFromContext(cookies: Array<{ name: string; httpOnly: boolean; secure: boolean; sameSite: string; path: string }>) {
  return cookies.find(c => c.name === 'guest_basket_id');
}

export async function safeJson(res: { json: () => Promise<any> }) {
  try { return await res.json(); } catch { return null; }
}
