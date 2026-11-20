// Chromatic's test captures a DOM snapshot at the end of every test (mock project only;
// see playwright.config.ts), through e2e/support/chromatic.ts, which keeps the bottom
// nav off a long page's content (#182). Otherwise identical to @playwright/test.
import { expect, test } from "./support/chromatic";
import { FIXTURE_ADMIN, FIXTURE_LEAGUE_ID, FIXTURE_MANAGER } from "../src/mocks/fixture";
import { logIn } from "./support/auth";
import { expectNoSeriousA11yViolations } from "./support/axe";

/**
 * Snapshots of the Manager and Admin pages (#187). The specs that use these
 * pages run the Manager in a second browser context, which Chromatic doesn't
 * archive, over data the whole suite shares. Here the persona signs in on the
 * test's own page and reads the mock's fixed world (src/mocks/fixture.ts), so
 * each snapshot is the same every run. It only looks, so a retry signs in with
 * the same address.
 */

test("Manage, from fixed data", async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== "mock", "the fixed data is the mock's");
  await logIn(page, testInfo, FIXTURE_MANAGER);
  await page.goto("/manage");

  await expect(page.getByRole("heading", { name: "Manage", level: 1 })).toBeVisible();
  await expect(page.getByRole("link", { name: "Winter Singles" })).toBeVisible();
  await expect(page.getByText("Nobody is waiting.")).toBeVisible();
  await expectNoSeriousA11yViolations(page);
});

test("a League's Manager page, from fixed data", async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== "mock", "the fixed data is the mock's");
  await logIn(page, testInfo, FIXTURE_MANAGER);
  await page.goto(`/manage/leagues/${FIXTURE_LEAGUE_ID}`);

  await expect(page.getByRole("heading", { name: "Winter Singles", level: 1 })).toBeVisible();
  await expect(page.getByRole("list", { name: "Players in the league" }).getByRole("listitem")).toHaveCount(2);
  await expectNoSeriousA11yViolations(page);
});

test("Admin, from fixed data", async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== "mock", "the fixed data is the mock's");
  await logIn(page, testInfo, FIXTURE_ADMIN);
  await page.goto("/admin");

  await expect(page.getByRole("heading", { name: "Admin", level: 1 })).toBeVisible();
  await expect(page.getByText("Users (7)")).toBeVisible();
  await expectNoSeriousA11yViolations(page);
});
