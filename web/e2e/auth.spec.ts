// Chromatic's test captures a DOM snapshot at the end of every test (mock project only;
// see playwright.config.ts). Otherwise identical to @playwright/test.
import { expect, test } from "@chromatic-com/playwright";
import { logIn, personaEmail, requestSignInLink } from "./support/auth";
import { expectNoSeriousA11yViolations } from "./support/axe";

test("an anonymous visitor is sent to the login page", async ({ page }) => {
  await page.goto("/");
  await expect(page).toHaveURL(/\/login$/);
  await expect(page).toHaveTitle("Log in · Social Tennis");
  await expectNoSeriousA11yViolations(page);
});

test("opening the emailed link asks for a button press before signing in", async ({ page }, testInfo) => {
  await page.goto(await requestSignInLink(page, testInfo, personaEmail(testInfo, "player")));
  await expect(page).toHaveTitle("Sign in · Social Tennis");
  await expect(page.getByRole("button", { name: "Sign in", exact: true })).toBeVisible();
  await expectNoSeriousA11yViolations(page);

  // Opening the link signed nobody in.
  await page.goto("/");
  await expect(page).toHaveURL(/\/login$/);
});

test("a link a mail scanner has already fetched still signs the User in", async ({ page }, testInfo) => {
  test.skip(testInfo.project.name === "mock", "the mocked API redeems any token any number of times");
  const link = await requestSignInLink(page, testInfo, personaEmail(testInfo, "player"));

  // A scanner follows the link with a GET, and doesn't press buttons.
  expect((await page.request.get(link)).ok()).toBe(true);

  await page.goto(link);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Home", level: 1 })).toBeVisible();
});

test("logging in lands on Home, and logging out ends the session", async ({ page }, testInfo) => {
  await logIn(page, testInfo, personaEmail(testInfo, "player"));
  await expect(page).toHaveTitle("Social Tennis");
  await expect(page.getByRole("heading", { name: "Clubs" })).toBeVisible();
  await expectNoSeriousA11yViolations(page);

  await page.getByRole("link", { name: "Profiles" }).click();
  await page.getByRole("button", { name: "Log out" }).click();
  await expect(page).toHaveURL(/\/login$/);

  await page.goto("/");
  await expect(page).toHaveURL(/\/login$/);
});
