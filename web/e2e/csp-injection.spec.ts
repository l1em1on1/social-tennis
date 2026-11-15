// Chromatic's test captures a DOM snapshot at the end of every test (mock project only;
// see playwright.config.ts). Otherwise identical to @playwright/test.
// Its own file: the browser flag below can only be set for a whole file.
import { expect, test } from "@chromatic-com/playwright";
import { cspViolations, recordCspViolations } from "./support/csp";

/**
 * What an injection would look like: markup in the HTML the server sends, with
 * no nonce. The response is changed in flight, headers kept, so the browser
 * applies the real policy to it. (A script added from page.evaluate is no test:
 * DevTools-injected code is outside the page's policy, and 'strict-dynamic'
 * trusts scripts that running code inserts.)
 *
 * Chrome doesn't know which server a fulfilled page came from, so it treats it
 * as public, and its local-network protection then refuses every request the
 * page makes to the web container's private address: no CSS, no fonts, none of
 * the app's own scripts (#135). The check is off for this test alone. It has
 * nothing to do with the CSP, which still applies in full.
 */
test.use({
  launchOptions: { args: ["--disable-features=LocalNetworkAccessChecks,BlockInsecurePrivateNetworkRequests"] },
});

test("an inline script in the page without the nonce is blocked", async ({ page }) => {
  await recordCspViolations(page);
  const failedRequests: string[] = [];
  page.on("requestfailed", (request) => failedRequests.push(request.url()));
  await page.route("**/login", async (route) => {
    const response = await route.fetch();
    const html = (await response.text()).replace(
      "</body>",
      "<script>window.injectedScriptRan = true;</script></body>",
    );
    await route.fulfill({ response, body: html });
  });

  await page.goto("/login");
  await page.waitForLoadState("networkidle");

  // The page's own (nonced) scripts ran: Next's client runtime is there,
  // and every asset loaded.
  const hydrated = await page.evaluate(
    () => typeof (window as unknown as { next?: { version?: string } }).next?.version === "string",
  );
  expect(hydrated, "the app's own scripts ran").toBe(true);
  expect(failedRequests).toEqual([]);

  const ran = await page.evaluate(() => (window as unknown as { injectedScriptRan?: boolean }).injectedScriptRan === true);
  expect(ran).toBe(false);
  expect(await cspViolations(page)).toEqual(["script-src-elem"]);
});
