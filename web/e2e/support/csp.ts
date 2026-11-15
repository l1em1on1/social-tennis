import type { Page } from "@playwright/test";

/** Records CSP violations from the first byte of every page. Init scripts run outside the page's CSP. */
export async function recordCspViolations(page: Page) {
  await page.addInitScript(() => {
    const violations: string[] = [];
    (window as unknown as { cspViolations: string[] }).cspViolations = violations;
    document.addEventListener("securitypolicyviolation", (event) => violations.push(event.effectiveDirective));
  });
}

export const cspViolations = (page: Page) =>
  page.evaluate(() => (window as unknown as { cspViolations: string[] }).cspViolations);
