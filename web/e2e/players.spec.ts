// Chromatic's test captures a DOM snapshot at the end of every test (mock project only;
// see playwright.config.ts). Otherwise identical to @playwright/test.
import { expect, test } from "@chromatic-com/playwright";
import { addFirstPlayer, logIn, personaEmail } from "./support/auth";
import { expectNoSeriousA11yViolations } from "./support/axe";

test("a first sign-in asks for a name, and the name can be changed in Profiles", async ({ page }, testInfo) => {
  await logIn(page, testInfo, personaEmail(testInfo, "player"));

  // Nobody to play as yet: no "Playing as", and the question is on Home.
  await expect(page.getByTestId("acting-player")).toHaveCount(0);
  await expect(page.getByRole("heading", { name: "Welcome! What's your name?" })).toBeVisible();
  await expectNoSeriousA11yViolations(page);

  await addFirstPlayer(page, "Sam Smith");
  await expect(page.getByRole("heading", { name: "Welcome! What's your name?" })).toHaveCount(0);

  await page.getByRole("navigation", { name: "Main" }).getByRole("link", { name: "Profiles" }).click();
  await page.getByRole("button", { name: "Rename Sam Smith" }).click();
  const sheet = page.getByRole("dialog", { name: "Rename Sam Smith" });
  await expectNoSeriousA11yViolations(page);
  await sheet.getByLabel("Name").fill("Samantha Smith");
  await sheet.getByRole("button", { name: "Save name" }).click();

  await expect(page.getByTestId("acting-player")).toHaveText("Samantha Smith");
  await expect(page.getByRole("dialog")).toHaveCount(0);

  // It is stored, not just shown.
  await page.reload();
  await expect(page.getByTestId("acting-player")).toHaveText("Samantha Smith");
});

test("a name of only spaces is refused with a message", async ({ page }, testInfo) => {
  await logIn(page, testInfo, personaEmail(testInfo, "player"));
  await page.getByLabel("Your name").fill("   ");
  await page.getByRole("button", { name: "Save my name" }).click();

  await expect(page.getByText("That name didn't work")).toBeVisible();
  await expect(page.getByTestId("acting-player")).toHaveCount(0);
});

test("a Player asks to join a Club, and is a member once a Manager approves", async ({ page, browser }, testInfo) => {
  await logIn(page, testInfo, personaEmail(testInfo, "player"));

  // No Player, nobody to ask as.
  await expect(page.getByRole("button", { name: /^Ask to join / })).toHaveCount(0);
  await addFirstPlayer(page, "Robin Requester");

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
  await manager.getByRole("button", { name: "Approve Robin Requester" }).click();
  await expect(manager.getByRole("button", { name: "Approve Robin Requester" })).toHaveCount(0);
  await managerContext.close();

  await page.reload();
  await expect(row).toContainText("Member");
  await page.getByRole("navigation", { name: "Main" }).getByRole("link", { name: "Profiles" }).click();
  await expect(page.getByText(`Member of ${clubName}`)).toBeVisible();
  await expectNoSeriousA11yViolations(page);
});

test("a Manager declines a request, and the Player can ask again", async ({ page, browser }, testInfo) => {
  await logIn(page, testInfo, personaEmail(testInfo, "player"));
  await addFirstPlayer(page, "Dana Declined");
  await page.getByRole("button", { name: /^Ask to join / }).first().click();
  await expect(page.getByText("Waiting for approval")).toBeVisible();

  const managerContext = await browser.newContext();
  const manager = await managerContext.newPage();
  await logIn(manager, testInfo, personaEmail(testInfo, "manager"));
  await manager.goto("/manage");
  await manager.getByRole("button", { name: "Decline Dana Declined" }).click();
  await expect(manager.getByText("Dana Declined")).toHaveCount(0);
  await managerContext.close();

  await page.reload();
  await expect(page.getByRole("button", { name: /^Ask to join / }).first()).toBeVisible();
});

test("someone a Manager invited by email is in the Club as soon as they save their name", async ({
  page,
  browser,
}, testInfo) => {
  const email = personaEmail(testInfo, "invited");

  const managerContext = await browser.newContext();
  const manager = await managerContext.newPage();
  await logIn(manager, testInfo, personaEmail(testInfo, "manager"));
  await manager.goto("/manage");
  await manager.getByLabel("Email address").first().fill(email);
  await manager.getByRole("button", { name: "Invite" }).first().click();
  await expect(manager.getByRole("status")).toContainText(`${email} is invited`);
  await expect(manager.getByRole("listitem").filter({ hasText: email })).toContainText("Not signed in yet");
  await managerContext.close();

  await logIn(page, testInfo, email);
  await addFirstPlayer(page, "Ivy Invited");
  await expect(page.getByRole("listitem").filter({ hasText: "Member" }).first()).toBeVisible();
  await expect(page.getByRole("button", { name: /^Ask to join / })).toHaveCount(0);
});

test("a Player can't open the Manager's members list", async ({ page }, testInfo) => {
  await logIn(page, testInfo, personaEmail(testInfo, "player"));
  await page.goto("/manage");
  await expect(page.getByRole("heading", { name: "Page not found" })).toBeVisible();
});
