// Chromatic's test captures a DOM snapshot at the end of every test (mock project only;
// see playwright.config.ts), through e2e/support/chromatic.ts, which keeps the bottom
// nav off a long page's content (#182). Otherwise identical to @playwright/test.
import { expect, test } from "./support/chromatic";
import { logIn, personaEmail } from "./support/auth";
import { expectNoSeriousA11yViolations } from "./support/axe";

// The privacy notice (#51): public, linked from the login page and the app shell.

test("the privacy notice opens from the login page without signing in", async ({ page }) => {
  await page.goto("/login");
  await page.getByRole("link", { name: "Privacy notice" }).click();

  await expect(page).toHaveURL(/\/privacy$/);
  await expect(page.getByRole("heading", { name: "Privacy notice", level: 1 })).toBeVisible();
  // From configuration: compose sets this address for the e2e builds. Shown
  // under "Who we are" and under deleting your data.
  const contact = page.getByRole("link", { name: "privacy@example.com" });
  await expect(contact).toHaveCount(2);
  await expect(contact.first()).toHaveAttribute("href", "mailto:privacy@example.com");
  await expectNoSeriousA11yViolations(page);
});

test.describe("signed in", () => {
  // Ends on the same page as the test above, which takes its snapshot.
  test.use({ disableAutoSnapshot: true });

  test("the app shell links to the privacy notice", async ({ page }, testInfo) => {
    await logIn(page, testInfo, personaEmail(testInfo, "player"));
    await page.getByRole("main").getByRole("link", { name: "Privacy notice" }).click();

    await expect(page.getByRole("heading", { name: "Privacy notice", level: 1 })).toBeVisible();
  });
});
