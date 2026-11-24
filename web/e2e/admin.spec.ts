// Chromatic's test captures a DOM snapshot at the end of every test (mock project only;
// see playwright.config.ts), through e2e/support/chromatic.ts, which keeps the bottom
// nav off a long page's content (#182). Otherwise identical to @playwright/test.
import { randomBytes } from "node:crypto";
import { expect, takeSnapshot, test } from "./support/chromatic";
import type { Page } from "@playwright/test";
import { Client } from "pg";
import { logIn, personaEmail, uniqueName } from "./support/auth";
import { expectNoSeriousA11yViolations } from "./support/axe";

const mainNav = (page: Page) => page.getByRole("navigation", { name: "Main" });

/** The Users row for exactly this email: "admin@example.com" must not match "manager-admin@example.com". */
const userRow = (page: Page, email: string) =>
  page.getByRole("listitem").filter({ has: page.getByText(email, { exact: true }) });

/** Opens a User's roles sheet from their row (#116) and returns it. Named by their email: these Users hold no Player. */
async function rolesOf(page: Page, email: string) {
  await userRow(page, email).getByRole("button", { name: `Roles for ${email}` }).click();
  const sheet = page.getByRole("dialog", { name: `Roles for ${email}` });
  await expect(sheet).toBeVisible();
  return sheet;
}

/**
 * On Admin, searches for this email and returns its row. The Users list is
 * paged, newest first (#134), so a User may not be on the first page.
 */
async function findUser(page: Page, email: string) {
  await page.goto("/admin");
  await page.getByLabel("Find a user").fill(email);
  await page.getByRole("button", { name: "Search", exact: true }).click();
  await expect(page).toHaveURL(/[?&]q=/);
  return userRow(page, email);
}

/**
 * Real API only: adds Users with these emails straight to the database, each a
 * second older than the one before, so newest first is the order given.
 */
async function addUsers(emails: string[]) {
  const client = new Client({ connectionString: process.env.DATABASE_URL });
  await client.connect();
  try {
    await client.query(
      `INSERT INTO "Users" ("Id", "Email", "CreatedAt", "Roles")
       SELECT gen_random_uuid(), email, now() - make_interval(secs => n), 0
       FROM unnest($1::text[]) WITH ORDINALITY AS added(email, n)`,
      [emails],
    );
  } finally {
    await client.end();
  }
}

// The Admin page lists every User and Club in a store the whole suite shares,
// so its snapshot would differ from run to run. The one exception is taken
// explicitly: a search that matches a single User (#134).
test.use({ disableAutoSnapshot: true });

test("an Admin creates a Club and renames it; on Manage its join link is turned on, reset and turned off", async ({
  page,
}, testInfo) => {
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

  // Managers see it, under its new name, with its join link off (#222). Players don't list Clubs.
  await mainNav(page).getByRole("link", { name: "Manage" }).click();
  const card = page.locator("section").filter({ has: page.getByRole("heading", { level: 2, name: renamed, exact: true }) });
  const joinLink = card.getByRole("region", { name: "Join link" });
  await expect(joinLink).toContainText("Off.");

  await joinLink.getByRole("button", { name: "Turn on the join link" }).click();
  const field = joinLink.getByLabel(`Link to ask to join ${renamed}`);
  await expect(field).toHaveValue(/\/join\/[A-Za-z0-9_-]{22}$/);
  const first = await field.inputValue();
  await expectNoSeriousA11yViolations(page);

  // Reset: a new link, and the old one stops working.
  await joinLink.getByRole("button", { name: "Reset link" }).click();
  await expect(field).not.toHaveValue(first);
  await page.goto(new URL(first).pathname);
  await expect(page.getByRole("heading", { name: "This link doesn't work any more" })).toBeVisible();

  // Off: no link at all.
  await page.goto("/manage");
  await joinLink.getByRole("button", { name: "Turn off" }).click();
  await expect(joinLink).toContainText("Off.");
  await expect(field).toHaveCount(0);
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
  await (await rolesOf(admin, email)).getByRole("button", { name: `Make manager of ${email}` }).click();
  await expect(row.getByText("Manager", { exact: true })).toBeVisible();

  await page.reload();
  await expect(mainNav(page).getByRole("link")).toHaveText(["Home", "Leagues", "Profiles", "Manage"]);
  await mainNav(page).getByRole("link", { name: "Manage" }).click();
  await expect(page.getByRole("heading", { name: "Manage", level: 1 })).toBeVisible();

  await (await rolesOf(admin, email)).getByRole("button", { name: `Remove manager from ${email}` }).click();
  await expect(row.getByText("Manager", { exact: true })).toHaveCount(0);
  await adminContext.close();

  await page.reload();
  await expect(page.getByRole("heading", { name: "Page not found" })).toBeVisible();
});

test("an Admin makes another User an Admin from their roles sheet, and can take it back", async ({
  page,
  browser,
}, testInfo) => {
  const email = personaEmail(testInfo, "deputy");
  await logIn(page, testInfo, email);

  const adminContext = await browser.newContext();
  const admin = await adminContext.newPage();
  await logIn(admin, testInfo, personaEmail(testInfo, "admin"));
  const row = await findUser(admin, email);
  // The sheet says what an Admin can do: opening it is the step that asks first.
  const sheet = await rolesOf(admin, email);
  await expect(sheet).toContainText("An Admin can also create clubs and change anyone's roles");
  await expectNoSeriousA11yViolations(admin);
  await sheet.getByRole("button", { name: `Make admin of ${email}` }).click();
  await expect(row.getByText("Admin", { exact: true })).toBeVisible();

  await page.reload();
  await expect(mainNav(page).getByRole("link")).toHaveText(["Home", "Leagues", "Profiles", "Manage", "Admin"]);

  await (await rolesOf(admin, email)).getByRole("button", { name: `Remove admin from ${email}` }).click();
  await expect(row.getByText("Admin", { exact: true })).toHaveCount(0);
  await adminContext.close();

  await page.reload();
  await expect(mainNav(page).getByRole("link")).toHaveText(["Home", "Leagues", "Profiles"]);
});

test("an Admin removes a User after confirming: their row goes, and their sign-in ends", async ({
  page,
  browser,
}, testInfo) => {
  const email = personaEmail(testInfo, "leaving");
  await logIn(page, testInfo, email);

  const adminContext = await browser.newContext();
  const admin = await adminContext.newPage();
  await logIn(admin, testInfo, personaEmail(testInfo, "admin"));
  const row = await findUser(admin, email);
  await row.getByRole("button", { name: `Remove ${email}` }).click();
  const sheet = admin.getByRole("dialog", { name: `Remove ${email}?` });
  await expect(sheet).toContainText("Their players show as “Former player” in results");
  await expectNoSeriousA11yViolations(admin);
  await sheet.getByRole("button", { name: "Remove user" }).click();
  await expect(row).toHaveCount(0);
  await expect(admin.getByText(`No one matches “${email}”.`)).toBeVisible();
  await adminContext.close();

  // Their session went with them (#116).
  await page.reload();
  await expect(page).toHaveURL(/\/login/);
});

test("an Admin can't remove themselves", async ({ page }, testInfo) => {
  const email = personaEmail(testInfo, "self-admin");
  await logIn(page, testInfo, email);
  const row = await findUser(page, email);
  await expect(row).toContainText("(you)");
  await expect(row.getByRole("button", { name: `Remove ${email}` })).toHaveCount(0);
});

test("the Admin set in the app's configuration can't be demoted here", async ({ page }, testInfo) => {
  await logIn(page, testInfo, personaEmail(testInfo, "admin"));
  // admin@example.com: listed in the api's Auth__AdminEmails (docker-compose.yml), and the mock's equivalent.
  // A whole email is listed first, whatever newer addresses contain it.
  const row = await findUser(page, "admin@example.com");
  await expect(row).toContainText("Admin in the app's configuration");
  await expect(row.getByRole("button")).toHaveCount(0);
});

test("an Admin finds a User by searching, one page at a time", async ({ page }, testInfo) => {
  // The view Chromatic reviews (#134). Mock only: only the mock project's
  // archives are uploaded, and there an address with "admin" in it is an
  // Admin. Nobody else's address contains this one, so it is always one row.
  test.skip(testInfo.project.name !== "mock", "Visual review runs on the mock project");
  const email = personaEmail(testInfo, "search-admin");
  await logIn(page, testInfo, email);

  const row = await findUser(page, email);
  await expect(row).toContainText("(you)");
  await expect(page.getByText(`1–1 of 1 matching “${email}”`)).toBeVisible();
  await expect(page.getByRole("navigation", { name: "Pages of users" })).toHaveCount(0);
  await takeSnapshot(page, "Users, searched for one address", testInfo);
});

test("an Admin pages through a search with Next and Previous", async ({ page }, testInfo) => {
  // A page is 50 Users, so this needs 51 that match. Only the real database can
  // be given them directly; the mock makes a User only when someone signs in.
  test.skip(testInfo.project.name === "mock", "Needs 51 Users, added straight to the database");
  const tag = `pager-${randomBytes(4).toString("hex")}`;
  await addUsers(Array.from({ length: 51 }, (_, i) => `${tag}-${i}@example.com`));
  await logIn(page, testInfo, personaEmail(testInfo, "admin"));
  await page.goto("/admin");
  await page.getByLabel("Find a user").fill(tag);
  await page.getByRole("button", { name: "Search", exact: true }).click();
  await expect(page.getByText(`1–50 of 51 matching “${tag}”`)).toBeVisible();

  // Next keeps the search and moves on a page; Previous comes back to the first.
  const pager = page.getByRole("navigation", { name: "Pages of users" });
  await expect(pager.getByRole("link", { name: "Previous" })).toHaveCount(0);
  await pager.getByRole("link", { name: "Next" }).click();
  await expect(page.getByText(`51–51 of 51 matching “${tag}”`)).toBeVisible();
  await expect(page).toHaveURL(new RegExp(`[?&]q=${tag}(&|$)`));
  await expect(page).toHaveURL(/[?&]offset=50(&|$)/);
  await expect(userRow(page, `${tag}-50@example.com`)).toBeVisible();
  await expect(pager.getByRole("link", { name: "Next" })).toHaveCount(0);
  await pager.getByRole("link", { name: "Previous" }).click();
  await expect(page.getByText(`1–50 of 51 matching “${tag}”`)).toBeVisible();
  await expect(page).toHaveURL(new RegExp(`[?&]q=${tag}(&|$)`));
  await expect(page).not.toHaveURL(/[?&]offset=/);

  // An offset past what the API takes is an empty page, not a refused search,
  // and Previous from there goes to the last page (#167's review).
  await page.goto(`/admin?q=${tag}&offset=9999999999`);
  await expect(page.getByText("No users on this page.")).toBeVisible();
  await expect(page.getByText("That search didn't work")).toHaveCount(0);
  await pager.getByRole("link", { name: "Previous" }).click();
  await expect(page.getByText(`51–51 of 51 matching “${tag}”`)).toBeVisible();
});

test("a Manager can't open the Admin page", async ({ page }, testInfo) => {
  await logIn(page, testInfo, personaEmail(testInfo, "manager"));
  await page.goto("/admin");
  await expect(page.getByRole("heading", { name: "Page not found" })).toBeVisible();
});
