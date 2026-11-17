// Chromatic's test captures a DOM snapshot at the end of every test (mock project only;
// see playwright.config.ts). Otherwise identical to @playwright/test.
import { expect, test } from "@chromatic-com/playwright";
import { addFirstPlayer, logIn, personaEmail, uniqueName } from "./support/auth";
import { expectNoSeriousA11yViolations } from "./support/axe";

test("a first sign-in asks for a name; name, gender and level are edited in Profiles", async ({ page }, testInfo) => {
  await logIn(page, testInfo, personaEmail(testInfo, "player"));

  // Nobody to play as yet: no "Playing as", and the question is on Home.
  await expect(page.getByTestId("acting-player")).toHaveCount(0);
  await expect(page.getByRole("heading", { name: "Welcome! What's your name?" })).toBeVisible();
  await expectNoSeriousA11yViolations(page);

  await addFirstPlayer(page, "Sam Smith");
  await expect(page.getByRole("heading", { name: "Welcome! What's your name?" })).toHaveCount(0);

  await page.getByRole("navigation", { name: "Main" }).getByRole("link", { name: "Profiles" }).click();
  await page.getByRole("button", { name: "Edit Sam Smith" }).click();
  const sheet = page.getByRole("dialog", { name: "Edit Sam Smith" });
  await expectNoSeriousA11yViolations(page);
  await sheet.getByLabel("Name").fill("Samantha Smith");
  await sheet.getByLabel("Plays as").selectOption("Female");
  await sheet.getByLabel("Level").selectOption("3");
  await sheet.getByRole("button", { name: "Save" }).click();

  await expect(page.getByTestId("acting-player")).toHaveText("Samantha Smith");
  await expect(page.getByRole("dialog")).toHaveCount(0);

  await expect(page.getByText("Female · Level 3")).toBeVisible();

  // It is stored, not just shown.
  await page.reload();
  await expect(page.getByTestId("acting-player")).toHaveText("Samantha Smith");
  await expect(page.getByText("Female · Level 3")).toBeVisible();
});

test("a name of only spaces is refused with a message", async ({ page }, testInfo) => {
  await logIn(page, testInfo, personaEmail(testInfo, "player"));
  await page.getByLabel("Your name").fill("   ");
  await page.getByRole("button", { name: "Save my name" }).click();

  await expect(page.getByText("That name didn't work")).toBeVisible();
  await expect(page.getByTestId("acting-player")).toHaveCount(0);
});

test("a Player asks to join a Club, and is a member once a Manager approves", async ({ page, browser }, testInfo) => {
  const robin = uniqueName(testInfo, "Robin Requester");
  await logIn(page, testInfo, personaEmail(testInfo, "player"));

  // No Player, nobody to ask as.
  await expect(page.getByRole("button", { name: /^Ask to join / })).toHaveCount(0);
  await addFirstPlayer(page, robin);

  const club = page.getByRole("listitem").filter({ has: page.getByRole("button", { name: /^Ask to join / }) }).first();
  const clubName = (await club.locator("span").first().textContent()) ?? "";
  await page.getByRole("button", { name: `Ask to join ${clubName}` }).click();
  const row = page.getByRole("listitem").filter({ hasText: clubName });
  await expect(row).toContainText("Waiting for approval");
  await expect(page.getByRole("button", { name: `Ask to join ${clubName}` })).toHaveCount(0);

  // A Manager, in a browser of their own.
  const managerContext = await browser.newContext();
  const manager = await managerContext.newPage();
  await logIn(manager, testInfo, personaEmail(testInfo, "manager"));
  await manager.getByRole("navigation", { name: "Main" }).getByRole("link", { name: "Manage" }).click();
  await expect(manager.getByRole("heading", { name: "Manage", level: 1 })).toBeVisible();
  await expectNoSeriousA11yViolations(manager);
  await manager.getByRole("button", { name: `Approve ${robin}` }).click();
  await expect(manager.getByRole("button", { name: `Approve ${robin}` })).toHaveCount(0);
  await managerContext.close();

  await page.reload();
  await expect(row).toContainText("Member");
  await page.getByRole("navigation", { name: "Main" }).getByRole("link", { name: "Profiles" }).click();
  await expect(page.getByText(`Member of ${clubName}`, { exact: true })).toBeVisible();
  await expectNoSeriousA11yViolations(page);
});

test("a Manager declines a request, and the Player can ask again", async ({ page, browser }, testInfo) => {
  const dana = uniqueName(testInfo, "Dana Declined");
  await logIn(page, testInfo, personaEmail(testInfo, "player"));
  await addFirstPlayer(page, dana);
  await page.getByRole("button", { name: /^Ask to join / }).first().click();
  await expect(page.getByText("Waiting for approval")).toBeVisible();

  const managerContext = await browser.newContext();
  const manager = await managerContext.newPage();
  await logIn(manager, testInfo, personaEmail(testInfo, "manager"));
  await manager.goto("/manage");
  await manager.getByRole("button", { name: `Decline ${dana}` }).click();
  await expect(manager.getByText(dana)).toHaveCount(0);
  await managerContext.close();

  await page.reload();
  await expect(page.getByRole("button", { name: /^Ask to join / }).first()).toBeVisible();
});

test("someone a Manager invited by email is in the Club as soon as they save their name", async ({
  page,
  browser,
}, testInfo) => {
  const ivy = uniqueName(testInfo, "Ivy Invited");
  const email = personaEmail(testInfo, "invited");

  const managerContext = await browser.newContext();
  const manager = await managerContext.newPage();
  await logIn(manager, testInfo, personaEmail(testInfo, "manager"));
  await manager.goto("/manage");
  // The first Club listed: the Admin specs create others, which sort after it.
  const clubName = (await manager.getByRole("heading", { level: 2 }).first().textContent()) ?? "";
  await manager.getByLabel("Email address", { exact: true }).first().fill(email);
  await manager.getByRole("button", { name: "Pre-approve", exact: true }).first().click();
  await expect(manager.getByRole("status")).toContainText(`${email} is pre-approved`);
  const invites = manager.getByRole("list", { name: "Pre-approved addresses" });
  const invite = invites.getByRole("listitem").filter({ hasText: email });
  await expect(invite).toBeVisible();
  // The mock keeps its invites until its server restarts, so a second local
  // run finds this address already joined. The real API gets a new address.
  if (testInfo.project.name === "real") {
    await expect(invite).toContainText("Not joined yet");
  }
  await managerContext.close();

  await logIn(page, testInfo, email);
  await addFirstPlayer(page, ivy);
  await expect(page.getByRole("listitem").filter({ hasText: clubName })).toContainText("Member");
  await expect(page.getByRole("button", { name: `Ask to join ${clubName}` })).toHaveCount(0);
});

test("a Manager removes a member, and withdraws an invite", async ({ page, browser }, testInfo) => {
  const mona = uniqueName(testInfo, "Mona Member");
  await logIn(page, testInfo, personaEmail(testInfo, "player"));
  await addFirstPlayer(page, mona);
  await page.getByRole("button", { name: /^Ask to join / }).first().click();
  await expect(page.getByText("Waiting for approval")).toBeVisible();

  const managerContext = await browser.newContext();
  const manager = await managerContext.newPage();
  await logIn(manager, testInfo, personaEmail(testInfo, "manager"));
  await manager.goto("/manage");
  await manager.getByRole("button", { name: `Approve ${mona}` }).click();

  // Removing asks first.
  await manager.getByRole("button", { name: `Remove ${mona}` }).click();
  const sheet = manager.getByRole("dialog", { name: `Remove ${mona}?` });
  await expect(sheet).toBeVisible();
  await expectNoSeriousA11yViolations(manager);
  await sheet.getByRole("button", { name: "Remove from club" }).click();
  await expect(manager.getByText(mona)).toHaveCount(0);

  // An invite nobody has used can be taken back.
  const email = personaEmail(testInfo, "withdrawn");
  await manager.getByLabel("Email address", { exact: true }).first().fill(email);
  await manager.getByRole("button", { name: "Pre-approve", exact: true }).first().click();
  await expect(manager.getByRole("status")).toContainText(`${email} is pre-approved`);
  await manager.getByRole("button", { name: `Withdraw pre-approval for ${email}` }).click();
  await expect(manager.getByRole("list", { name: "Pre-approved addresses" }).getByText(email)).toHaveCount(0);
  await managerContext.close();

  await page.reload();
  await expect(page.getByRole("button", { name: /^Ask to join / }).first()).toBeVisible();
});

test("a Player takes back a request, and a member leaves a Club", async ({ page, browser }, testInfo) => {
  const lee = uniqueName(testInfo, "Lee Leaver");
  const nav = page.getByRole("navigation", { name: "Main" });
  await logIn(page, testInfo, personaEmail(testInfo, "player"));
  await addFirstPlayer(page, lee);

  // Ask, then think better of it.
  await page.getByRole("button", { name: /^Ask to join / }).first().click();
  await expect(page.getByText("Waiting for approval")).toBeVisible();
  await nav.getByRole("link", { name: "Profiles" }).click();
  await page.getByRole("button", { name: /^Cancel request to join / }).click();
  await expect(page.getByText("Not in a club yet")).toBeVisible();

  // Ask again, and this time a Manager approves.
  await nav.getByRole("link", { name: "Home" }).click();
  await page.getByRole("button", { name: /^Ask to join / }).first().click();
  await expect(page.getByText("Waiting for approval")).toBeVisible();
  const managerContext = await browser.newContext();
  const manager = await managerContext.newPage();
  await logIn(manager, testInfo, personaEmail(testInfo, "manager"));
  await manager.goto("/manage");
  await manager.getByRole("button", { name: `Approve ${lee}` }).click();
  await expect(manager.getByRole("button", { name: `Remove ${lee}` })).toBeVisible();
  await managerContext.close();

  await nav.getByRole("link", { name: "Profiles" }).click();
  await page.getByRole("button", { name: /^Leave / }).click();
  const sheet = page.getByRole("dialog", { name: /^Leave .+\?$/ });
  await expect(sheet).toBeVisible();
  await expectNoSeriousA11yViolations(page);
  await sheet.getByRole("button", { name: "Leave club" }).click();
  await expect(page.getByText("Not in a club yet")).toBeVisible();
});

test("a Manager adds a Player by name and email, rates them, and they hold it on signing in", async ({
  page,
  browser,
}, testInfo) => {
  const child = uniqueName(testInfo, "Casey Child");
  const email = personaEmail(testInfo, "newparent");

  const managerContext = await browser.newContext();
  const manager = await managerContext.newPage();
  await logIn(manager, testInfo, personaEmail(testInfo, "manager"));
  await manager.goto("/manage");
  // The first Club listed: the Admin specs create others, which sort after it.
  const clubName = (await manager.getByRole("heading", { level: 2 }).first().textContent()) ?? "";
  await manager.getByLabel("Player's name").first().fill(child);
  await manager.getByLabel("Their email address").first().fill(email);
  await manager.getByRole("button", { name: "Add player" }).first().click();
  await expect(manager.getByRole("status")).toContainText(`${child} is added`);

  // Listed under the login that will hold it, which nobody has used yet.
  const held = manager.getByRole("list", { name: `Players of ${email}` }).first();
  await expect(held.getByText(child, { exact: true }).first()).toBeVisible();

  // A new Player's Rating is 1; the Manager sets it by hand.
  await held.getByRole("button", { name: `Rating 1 for ${child}` }).click();
  const sheet = manager.getByRole("dialog", { name: `Rating for ${child}` });
  await expectNoSeriousA11yViolations(manager);
  await sheet.getByLabel("Rating").fill("7");
  await sheet.getByRole("button", { name: "Save rating" }).click();
  await expect(held.getByRole("button", { name: `Rating 7 for ${child}` })).toBeVisible();
  await managerContext.close();

  // Signing in with that address: the Player is theirs, already a member, with no name to ask for.
  await logIn(page, testInfo, email);
  await expect(page.getByTestId("acting-player")).toHaveText(child);
  await expect(page.getByRole("heading", { name: "Welcome! What's your name?" })).toHaveCount(0);
  await expect(page.getByRole("listitem").filter({ hasText: clubName })).toContainText("Member");

  // Their Rating is the Manager's business: Profiles neither shows nor edits it.
  await page.getByRole("navigation", { name: "Main" }).getByRole("link", { name: "Profiles" }).click();
  await expect(page.getByText(child, { exact: true }).first()).toBeVisible();
  await expect(page.getByText(/rating/i)).toHaveCount(0);
  await page.getByRole("button", { name: `Edit ${child}` }).click();
  await expect(page.getByRole("dialog", { name: `Edit ${child}` }).getByLabel(/rating/i)).toHaveCount(0);
});

test("a Player can't open the Manager's members list", async ({ page }, testInfo) => {
  await logIn(page, testInfo, personaEmail(testInfo, "player"));
  await page.goto("/manage");
  await expect(page.getByRole("heading", { name: "Page not found" })).toBeVisible();
});
