// Chromatic's test captures a DOM snapshot at the end of every test (mock project only;
// see playwright.config.ts). Otherwise identical to @playwright/test.
import { expect, test } from "@chromatic-com/playwright";
import type { Page } from "@playwright/test";
import { logIn, personaEmail, uniqueName } from "./support/auth";
import { expectNoSeriousA11yViolations } from "./support/axe";

const mainNav = (page: Page) => page.getByRole("navigation", { name: "Main" });

/** The Users row for exactly this email: "admin@example.com" must not match "manager-admin@example.com". */
const userRow = (page: Page, email: string) =>
  page.getByRole("listitem").filter({ has: page.getByText(email, { exact: true }) });

/**
 * On Admin, searches for this email and returns its row. The Users list is
 * paged, newest first (#134), so a User may not be on the first page.
 */
async function findUser(page: Page, email: string) {
  await page.goto("/admin");
  await page.getByLabel("Find a user").fill(email);
  await page.getByRole("button", { name: "Search" }).click();
  await expect(page).toHaveURL(/[?&]q=/);
  return userRow(page, email);
}

// The Admin page lists every User and Club in a store the whole suite shares,
// so its snapshot would differ from run to run.
test.use({ disableAutoSnapshot: true });

test("an Admin creates a Club and renames it", async ({ page }, testInfo) => {
  // Two axe scans of a page that lists every User: slow against a database many runs have filled.
  test.slow();
  // "Zz": sorts after the Club the other specs join, which they find as the first one listed.
  const name = uniqueName(testInfo, "Zz Hilltop Tennis Club");
  const renamed = uniqueName(testInfo, "Zz Valley Tennis Club");
  await logIn(page, testInfo, personaEmail(testInfo, "admin"));
  await mainNav(page).getByRole("link", { name: "Admin" }).click();
  await expect(page.getByRole("heading", { name: "Admin", level: 1 })).toBeVisible();
  await expectNoSeriousA11yViolations(page);

  await page.getByRole("button", { name: "Create a club" }).click();
  const create = page.getByRole("dialog", { name: "Create a club" });
  await expectNoSeriousA11yViolations(page);
  await create.getByLabel("Club name").fill(name);
  await create.getByRole("button", { name: "Create club" }).click();
  await expect(page.getByRole("listitem").filter({ hasText: name })).toBeVisible();
  await expect(page.getByRole("dialog")).toHaveCount(0);

  await page.getByRole("button", { name: `Rename ${name}` }).click();
  const rename = page.getByRole("dialog", { name: `Rename ${name}` });
  await rename.getByLabel("Club name").fill(renamed);
  await rename.getByRole("button", { name: "Save name" }).click();
  await expect(page.getByRole("listitem").filter({ hasText: renamed })).toBeVisible();
  await expect(page.getByText(name, { exact: true })).toHaveCount(0);

  // Everyone sees it, under its new name.
  await mainNav(page).getByRole("link", { name: "Home" }).click();
  await expect(page.getByRole("listitem").filter({ hasText: renamed })).toBeVisible();
});

test("a club name of only spaces is refused with a message", async ({ page }, testInfo) => {
  await logIn(page, testInfo, personaEmail(testInfo, "admin"));
  await page.goto("/admin");
  await page.getByRole("button", { name: "Create a club" }).click();
  const create = page.getByRole("dialog", { name: "Create a club" });
  await create.getByLabel("Club name").fill("   ");
  await create.getByRole("button", { name: "Create club" }).click();

  await expect(page.getByText("That club name didn't work")).toBeVisible();
});

test("an Admin makes a User a Manager, and it takes effect without signing in again", async ({
  page,
  browser,
}, testInfo) => {
  const email = personaEmail(testInfo, "promoted");
  await logIn(page, testInfo, email);
  await expect(mainNav(page).getByRole("link")).toHaveText(["Home", "Leagues", "Profiles"]);

  const adminContext = await browser.newContext();
  const admin = await adminContext.newPage();
  await logIn(admin, testInfo, personaEmail(testInfo, "admin"));
  const row = await findUser(admin, email);
  await expect(admin.getByText(`1–1 of 1 matching “${email}”`)).toBeVisible();
  await row.getByRole("button", { name: `Make manager of ${email}` }).click();
  await expect(row.getByText("Manager", { exact: true })).toBeVisible();

  await page.reload();
  await expect(mainNav(page).getByRole("link")).toHaveText(["Home", "Leagues", "Profiles", "Manage"]);
  await mainNav(page).getByRole("link", { name: "Manage" }).click();
  await expect(page.getByRole("heading", { name: "Manage", level: 1 })).toBeVisible();

  await row.getByRole("button", { name: `Remove manager from ${email}` }).click();
  await expect(row.getByRole("button", { name: `Make manager of ${email}` })).toBeVisible();
  await adminContext.close();

  await page.reload();
  await expect(page.getByRole("heading", { name: "Page not found" })).toBeVisible();
});

test("an Admin makes another User an Admin after confirming, and can take it back", async ({
  page,
  browser,
}, testInfo) => {
  const email = personaEmail(testInfo, "deputy");
  await logIn(page, testInfo, email);

  const adminContext = await browser.newContext();
  const admin = await adminContext.newPage();
  await logIn(admin, testInfo, personaEmail(testInfo, "admin"));
  const row = await findUser(admin, email);
  await row.getByRole("button", { name: `Make admin of ${email}` }).click();
  const sheet = admin.getByRole("dialog", { name: `Make ${email} an Admin?` });
  await expect(sheet).toBeVisible();
  await expectNoSeriousA11yViolations(admin);
  await sheet.getByRole("button", { name: "Make admin" }).click();
  await expect(row.getByText("Admin", { exact: true })).toBeVisible();

  await page.reload();
  await expect(mainNav(page).getByRole("link")).toHaveText(["Home", "Leagues", "Profiles", "Manage", "Admin"]);

  await row.getByRole("button", { name: `Remove admin from ${email}` }).click();
  await expect(row.getByRole("button", { name: `Make manager of ${email}` })).toBeVisible();
  await adminContext.close();

  await page.reload();
  await expect(mainNav(page).getByRole("link")).toHaveText(["Home", "Leagues", "Profiles"]);
});

test("the Admin set in the app's configuration can't be demoted here", async ({ page }, testInfo) => {
  await logIn(page, testInfo, personaEmail(testInfo, "admin"));
  // admin@example.com: listed in the api's Auth__AdminEmails (docker-compose.yml), and the mock's equivalent.
  // A whole email is listed first, whatever newer addresses contain it.
  const row = await findUser(page, "admin@example.com");
  await expect(row).toContainText("Admin in the app's configuration");
  await expect(row.getByRole("button")).toHaveCount(0);
});

test("a Manager can't open the Admin page", async ({ page }, testInfo) => {
  await logIn(page, testInfo, personaEmail(testInfo, "manager"));
  await page.goto("/admin");
  await expect(page.getByRole("heading", { name: "Page not found" })).toBeVisible();
});
