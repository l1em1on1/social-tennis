// Chromatic's test captures a DOM snapshot at the end of every test (mock project only;
// see playwright.config.ts). Otherwise identical to @playwright/test.
import { expect, test } from "@chromatic-com/playwright";
import type { Page } from "@playwright/test";
import { logIn, personaEmail } from "./support/auth";
import { expectNoSeriousA11yViolations } from "./support/axe";

const mainNav = (page: Page) => page.getByRole("navigation", { name: "Main" });

test("the shell fits a 360px screen with 44px tap targets", async ({ page }, testInfo) => {
  await logIn(page, testInfo, personaEmail(testInfo, "manager-admin"));

  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );
  expect(overflow, "horizontal scroll at 360px").toBeLessThanOrEqual(0);

  const links = mainNav(page).getByRole("link");
  await expect(links).toHaveCount(5);
  for (const link of await links.all()) {
    const box = await link.boundingBox();
    expect(box?.width).toBeGreaterThanOrEqual(44);
    expect(box?.height).toBeGreaterThanOrEqual(44);
  }
});

test("a Player without roles sees no Manage or Admin entry, and can't open them", async ({ page }, testInfo) => {
  await logIn(page, testInfo, personaEmail(testInfo, "player"));

  await expect(mainNav(page).getByRole("link")).toHaveText(["Home", "Ladder", "Profiles"]);
  // Asserted on the page, not the status: the page streams under a loading
  // boundary, so notFound() can arrive after a 200 has already been sent.
  for (const path of ["/manage", "/admin"]) {
    await page.goto(path);
    await expect(page.getByRole("heading", { name: "Page not found" })).toBeVisible();
  }
});

test("a Manager sees Manage; an Admin sees Admin", async ({ page }, testInfo) => {
  await logIn(page, testInfo, personaEmail(testInfo, "manager"));
  await expect(mainNav(page).getByRole("link")).toHaveText(["Home", "Ladder", "Profiles", "Manage"]);
  await mainNav(page).getByRole("link", { name: "Manage" }).click();
  await expect(page.getByRole("heading", { name: "Manage", level: 1 })).toBeVisible();
  await expect(mainNav(page).getByRole("link", { name: "Manage" })).toHaveAttribute("aria-current", "page");

  await page.context().clearCookies();
  await logIn(page, testInfo, personaEmail(testInfo, "admin"));
  await expect(mainNav(page).getByRole("link")).toHaveText(["Home", "Ladder", "Profiles", "Admin"]);
});

test("switching the acting Player survives navigation", async ({ page }, testInfo) => {
  await logIn(page, testInfo, personaEmail(testInfo, "parent"));
  const acting = page.getByTestId("acting-player");
  await expect(acting).toHaveText("Parent");

  await page.getByRole("button", { name: /switch player/ }).click();
  const sheet = page.getByRole("dialog", { name: "Play as" });
  await expect(sheet).toBeVisible();
  await expectNoSeriousA11yViolations(page);
  await sheet.getByRole("button", { name: "Alex Junior" }).click();
  await expect(acting).toHaveText("Alex Junior");

  await mainNav(page).getByRole("link", { name: "Ladder" }).click();
  await expect(page.getByRole("heading", { name: "Ladder", level: 1 })).toBeVisible();
  await expect(acting).toHaveText("Alex Junior");

  await page.reload();
  await expect(acting).toHaveText("Alex Junior");

  await mainNav(page).getByRole("link", { name: "Profiles" }).click();
  await expect(page.getByRole("listitem").filter({ hasText: "Alex Junior" })).toContainText("Playing as");
  await expectNoSeriousA11yViolations(page);
});
