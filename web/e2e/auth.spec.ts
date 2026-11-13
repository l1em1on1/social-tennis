import { expect, test } from "@playwright/test";
import { logIn, personaEmail } from "./support/auth";
import { expectNoSeriousA11yViolations } from "./support/axe";

test("an anonymous visitor is sent to the login page", async ({ page }) => {
  await page.goto("/");
  await expect(page).toHaveURL(/\/login$/);
  await expect(page).toHaveTitle("Log in · Social Tennis");
  await expectNoSeriousA11yViolations(page);
});

test("logging in lands on Home, and logging out ends the session", async ({ page }, testInfo) => {
  await logIn(page, testInfo, personaEmail("player"));
  await expect(page).toHaveTitle("Social Tennis");
  await expect(page.getByRole("heading", { name: "Clubs" })).toBeVisible();
  await expectNoSeriousA11yViolations(page);

  await page.getByRole("link", { name: "Profiles" }).click();
  await page.getByRole("button", { name: "Log out" }).click();
  await expect(page).toHaveURL(/\/login$/);

  await page.goto("/");
  await expect(page).toHaveURL(/\/login$/);
});
