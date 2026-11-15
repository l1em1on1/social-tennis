// Chromatic's test captures a DOM snapshot at the end of every test (mock project only;
// see playwright.config.ts). Otherwise identical to @playwright/test.
import { expect, test } from "@chromatic-com/playwright";
import type { Response } from "@playwright/test";
import { logIn, personaEmail } from "./support/auth";
import { cspViolations, recordCspViolations } from "./support/csp";

/**
 * Security headers and the Content-Security-Policy (#53). Both E2E stacks are
 * served over plain http, so HSTS and upgrade-insecure-requests must be absent
 * here: they are sent only when PUBLIC_BASE_URL is https.
 */

function expectSecurityHeaders(response: Response | null) {
  expect(response, "the page answered").not.toBeNull();
  const headers = response!.headers();

  const csp = headers["content-security-policy"];
  expect(csp).toMatch(/script-src 'self' 'nonce-[A-Za-z0-9+/=]+' 'strict-dynamic'/);
  for (const directive of ["default-src 'self'", "object-src 'none'", "base-uri 'self'", "form-action 'self'", "frame-ancestors 'none'"]) {
    expect(csp).toContain(directive);
  }
  expect(csp).not.toContain("unsafe-inline");
  expect(csp).not.toContain("unsafe-eval");
  expect(csp).not.toContain("upgrade-insecure-requests");

  expect(headers["x-content-type-options"]).toBe("nosniff");
  expect(headers["referrer-policy"]).toBe("same-origin");
  expect(headers["x-frame-options"]).toBe("DENY");
  expect(headers["x-powered-by"]).toBeUndefined();
  expect(headers["strict-transport-security"]).toBeUndefined();
  return csp;
}

test("the login page sends the security headers", async ({ page }) => {
  expectSecurityHeaders(await page.goto("/login"));
});

test("a signed-in page sends the security headers, with a fresh nonce each time", async ({ page }, testInfo) => {
  await logIn(page, testInfo, personaEmail(testInfo, "player"));

  const first = expectSecurityHeaders(await page.goto("/"));
  const second = expectSecurityHeaders(await page.goto("/"));
  expect(second).not.toBe(first);
});

test("pages run under the policy with no violations", async ({ page }, testInfo) => {
  await recordCspViolations(page);

  await logIn(page, testInfo, personaEmail(testInfo, "player"));
  expect(await cspViolations(page), "signing in and loading Home broke no rule").toEqual([]);
});

test("the login page shows back only an email address, never other text", async ({ page }) => {
  await page.goto("/login?sent=someone%40example.com");
  await expect(page.getByText("A sign-in link is on its way to someone@example.com.")).toBeVisible();

  await page.goto(`/login?sent=${encodeURIComponent("Your account is locked. Call 0800 000 000")}`);
  await expect(page.getByRole("heading", { name: "Check your email" })).toHaveCount(0);
  await expect(page.getByLabel("Email address")).toBeVisible();
});
