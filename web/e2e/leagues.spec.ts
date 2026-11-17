// Chromatic's test captures a DOM snapshot at the end of every test (mock project only;
// see playwright.config.ts). Otherwise identical to @playwright/test.
import { expect, test } from "@chromatic-com/playwright";
import type { Page } from "@playwright/test";
import { logIn, personaEmail, uniqueName } from "./support/auth";
import { expectNoSeriousA11yViolations } from "./support/axe";

/** On Manage: adds a Player to the first Club for an email nobody has signed in with yet. */
async function addClubPlayer(manager: Page, name: string, email: string) {
  await manager.goto("/manage");
  await manager.getByLabel("Player's name").first().fill(name);
  await manager.getByLabel("Their email address").first().fill(email);
  await manager.getByRole("button", { name: "Add player" }).first().click();
  await expect(manager.getByRole("status")).toContainText(`${name} is added`);
}

/** From Manage: creates a League in the first Club with the setup form's defaults, and lands on its page. */
async function createLeague(manager: Page, name: string) {
  await manager.goto("/manage");
  // The first Club listed: the Admin specs create others, which sort after it.
  await manager.getByRole("link", { name: "New league" }).first().click();
  await expect(manager.getByRole("heading", { name: "New league", level: 1 })).toBeVisible();
  await manager.getByLabel("Name", { exact: true }).fill(name);
  await manager.getByLabel("Starts").fill("2026-12-01");
  await manager.getByLabel("Ends").fill("2027-03-31");
  await manager.getByRole("button", { name: "Create League" }).click();
  await expect(manager.getByRole("heading", { name, level: 1 })).toBeVisible();
}

test("a Manager creates a League; a Player asks to join and is in once approved", async ({
  page,
  browser,
}, testInfo) => {
  // Three sign-ins and several round trips: over the default timeout on a busy real-API run.
  test.slow();
  const league = uniqueName(testInfo, "Winter Singles");
  const robin = uniqueName(testInfo, "Robin League");
  const email = personaEmail(testInfo, "leaguer");

  const managerContext = await browser.newContext();
  const manager = await managerContext.newPage();
  await logIn(manager, testInfo, personaEmail(testInfo, "manager"));
  await addClubPlayer(manager, robin, email);
  await createLeague(manager, league);
  await expect(manager.getByRole("status")).toContainText(`${league} is created`);
  await expect(manager.getByText("One set of 6, tiebreak at 5–5")).toBeVisible();
  await expect(manager.getByText("Divisional · calendar month Stages · Manager closes")).toBeVisible();
  await expectNoSeriousA11yViolations(manager);

  // The Player, signing in with the email the Manager used, finds it to join.
  await logIn(page, testInfo, email);
  await page.getByRole("navigation", { name: "Main" }).getByRole("link", { name: "Leagues" }).click();
  await expect(page.getByRole("heading", { name: "Leagues", level: 1 })).toBeVisible();
  const joinable = page.getByRole("region", { name: "You can join" });
  await joinable.getByRole("button", { name: `Ask to join ${league}` }).click();
  const card = joinable.getByRole("region", { name: league });
  await expect(card).toContainText("Request sent");
  await expect(card.getByRole("button", { name: `Ask to join ${league}` })).toHaveCount(0);
  await expectNoSeriousA11yViolations(page);

  // The Manager sees it waiting, and approves it.
  await manager.reload();
  await manager.getByRole("button", { name: `Approve ${robin}` }).click();
  await expect(manager.getByRole("button", { name: `Approve ${robin}` })).toHaveCount(0);
  await expect(manager.getByRole("list", { name: "Players in the league" })).toContainText(robin);
  await managerContext.close();

  await page.reload();
  const mine = page.getByRole("region", { name: `${robin}'s leagues` });
  await expect(mine.getByRole("region", { name: league })).toContainText("Joined");
  await expect(joinable.getByRole("region", { name: league })).toHaveCount(0);
});

test("a rejected Player sees the outcome; a Manager adds a club member straight in", async ({
  page,
  browser,
}, testInfo) => {
  // Three sign-ins and several round trips: over the default timeout on a busy real-API run.
  test.slow();
  const league = uniqueName(testInfo, "Summer Ladder");
  const dana = uniqueName(testInfo, "Dana League");
  const ed = uniqueName(testInfo, "Ed League");
  const danaEmail = personaEmail(testInfo, "rejected");
  const edEmail = personaEmail(testInfo, "added");

  const managerContext = await browser.newContext();
  const manager = await managerContext.newPage();
  await logIn(manager, testInfo, personaEmail(testInfo, "manager"));
  await addClubPlayer(manager, dana, danaEmail);
  await addClubPlayer(manager, ed, edEmail);
  await createLeague(manager, league);
  const leagueUrl = manager.url();

  await logIn(page, testInfo, danaEmail);
  await page.goto("/leagues");
  await page.getByRole("button", { name: `Ask to join ${league}` }).click();
  await expect(page.getByRole("region", { name: league })).toContainText("Request sent");

  await manager.goto(leagueUrl);
  await manager.getByRole("button", { name: `Reject ${dana}` }).click();
  await expect(manager.getByRole("list", { name: "Rejected requests" })).toContainText(dana);

  // Ed never asked: the Manager puts him in.
  await manager.getByLabel("Player", { exact: true }).selectOption({ label: `${ed} (${edEmail})` });
  await manager.getByRole("button", { name: "Add to league" }).click();
  const players = manager.getByRole("list", { name: "Players in the league" });
  await expect(players).toContainText(ed);
  await expect(players).not.toContainText(dana);

  // Dana, turned down, is added and then removed. Her rejection stays, so the
  // sheet doesn't promise she can ask again (#168's review), and the end of
  // this test shows she can't.
  await manager.getByLabel("Player", { exact: true }).selectOption({ label: `${dana} (${danaEmail})` });
  await manager.getByRole("button", { name: "Add to league" }).click();
  await expect(players).toContainText(dana);
  await manager.getByRole("button", { name: `Remove ${dana}` }).click();
  const sheet = manager.getByRole("dialog", { name: `Remove ${dana}?` });
  await expect(sheet).toContainText("they can't ask again, but you can add them back");
  await sheet.getByRole("button", { name: "Remove from league" }).click();
  await expect(players).not.toContainText(dana);
  await expectNoSeriousA11yViolations(manager);
  await managerContext.close();

  await page.reload();
  const card = page.getByRole("region", { name: "You can join" }).getByRole("region", { name: league });
  await expect(card).toContainText("Not accepted");
  await expect(card.getByRole("button", { name: `Ask to join ${league}` })).toHaveCount(0);
});

test("the League setup form keeps to the rules the API enforces", async ({ page }, testInfo) => {
  await logIn(page, testInfo, personaEmail(testInfo, "manager"));
  await page.goto("/manage");
  await page.getByRole("link", { name: "New league" }).first().click();

  // Foursomes and Knockout can't be chosen together, either way round.
  const knockout = page.getByRole("radio", { name: "Knockout" });
  await page.getByLabel("Type").selectOption("Foursomes");
  await expect(knockout).toBeDisabled();
  await page.getByLabel("Type").selectOption("DoublesMix");
  await knockout.check({ force: true });
  await expect(page.getByRole("option", { name: "Foursomes" })).toBeDisabled();

  // Roll mode is a Divisional League's only.
  await expect(page.getByRole("group", { name: "When a Stage ends" })).toHaveCount(0);
  await page.getByRole("radio", { name: "Divisional ladder" }).check({ force: true });
  await expect(page.getByRole("group", { name: "When a Stage ends" })).toBeVisible();

  // The Match Tiebreak switch only exists for best of three.
  const matchTiebreak = page.getByRole("checkbox", { name: /Match tiebreak decider/ });
  await expect(matchTiebreak).toHaveCount(0);
  await page.getByRole("radio", { name: "Best of 3" }).check({ force: true });
  await matchTiebreak.check();
  await expect(page.getByText("Best of 3 sets of 6, tiebreak at 5–5, match tiebreak decider")).toBeVisible();
  await page.getByRole("radio", { name: "One set" }).check({ force: true });
  await expect(matchTiebreak).toHaveCount(0);
  await expectNoSeriousA11yViolations(page);

  // The API's refusal shows by the field, and what was typed stays.
  const name = uniqueName(testInfo, "Too Short");
  await page.getByLabel("Name", { exact: true }).fill(name);
  await page.getByLabel("Starts").fill("2026-12-01");
  await page.getByLabel("Ends").fill("2026-12-01");
  await page.getByRole("button", { name: "Create League" }).click();
  await expect(page.getByText("The League has to end after it starts.")).toBeVisible();
  await expect(page.getByLabel("Name", { exact: true })).toHaveValue(name);
});

test("a Manager renames a League, and its Player sees the new name", async ({ page, browser }, testInfo) => {
  // Two sign-ins and several round trips: over the default timeout on a busy real-API run.
  test.slow();
  const league = uniqueName(testInfo, "Autumn Ladder");
  const renamed = uniqueName(testInfo, "Spring Ladder");
  const kim = uniqueName(testInfo, "Kim League");
  const email = personaEmail(testInfo, "renamed");

  const managerContext = await browser.newContext();
  const manager = await managerContext.newPage();
  await logIn(manager, testInfo, personaEmail(testInfo, "manager"));
  await addClubPlayer(manager, kim, email);
  await createLeague(manager, league);
  await manager.getByLabel("Player", { exact: true }).selectOption({ label: `${kim} (${email})` });
  await manager.getByRole("button", { name: "Add to league" }).click();
  await expect(manager.getByRole("list", { name: "Players in the league" })).toContainText(kim);

  // A blank name is refused, and the League keeps its name.
  await manager.getByRole("button", { name: `Rename ${league}` }).click();
  await manager.getByLabel("League name").fill("   ");
  await manager.getByRole("button", { name: "Save name" }).click();
  await expect(manager.getByText("That league name didn't work")).toBeVisible();
  await expect(manager.getByRole("heading", { name: league, level: 1 })).toBeVisible();

  await manager.getByRole("button", { name: `Rename ${league}` }).click();
  await manager.getByLabel("League name").fill(renamed);
  await manager.getByRole("button", { name: "Save name" }).click();
  await expect(manager.getByRole("heading", { name: renamed, level: 1 })).toBeVisible();
  await expect(manager.getByRole("list", { name: "Players in the league" })).toContainText(kim);
  await expectNoSeriousA11yViolations(manager);
  await managerContext.close();

  await logIn(page, testInfo, email);
  await page.goto("/leagues");
  const mine = page.getByRole("region", { name: `${kim}'s leagues` });
  await expect(mine.getByRole("region", { name: renamed })).toContainText("Joined");
  await expect(page.getByRole("region", { name: league, exact: true })).toHaveCount(0);
});

test("a Manager removes a Player from a League, and they can ask to join again", async ({ page, browser }, testInfo) => {
  // Two sign-ins and several round trips: over the default timeout on a busy real-API run.
  test.slow();
  const league = uniqueName(testInfo, "Removal Ladder");
  const lee = uniqueName(testInfo, "Lee League");
  const email = personaEmail(testInfo, "removed");

  const managerContext = await browser.newContext();
  const manager = await managerContext.newPage();
  await logIn(manager, testInfo, personaEmail(testInfo, "manager"));
  await addClubPlayer(manager, lee, email);
  await createLeague(manager, league);
  await manager.getByLabel("Player", { exact: true }).selectOption({ label: `${lee} (${email})` });
  await manager.getByRole("button", { name: "Add to league" }).click();
  const players = manager.getByRole("list", { name: "Players in the league" });
  await expect(players).toContainText(lee);

  await manager.getByRole("button", { name: `Remove ${lee}` }).click();
  const sheet = manager.getByRole("dialog", { name: `Remove ${lee}?` });
  await expect(sheet).toContainText("They can ask to join again");
  await expectNoSeriousA11yViolations(manager);
  await sheet.getByRole("button", { name: "Remove from league" }).click();
  await expect(manager.getByText("No players yet")).toBeVisible();
  await managerContext.close();

  await logIn(page, testInfo, email);
  await page.goto("/leagues");
  const joinable = page.getByRole("region", { name: "You can join" });
  await expect(joinable.getByRole("button", { name: `Ask to join ${league}` })).toBeVisible();
  await expect(page.getByRole("region", { name: `${lee}'s leagues` }).getByRole("region", { name: league })).toHaveCount(0);
});

test("a Player can't open a Manager's League pages", async ({ page }, testInfo) => {
  await logIn(page, testInfo, personaEmail(testInfo, "player"));
  await page.goto("/manage/leagues/new");
  await expect(page.getByRole("heading", { name: "Page not found" })).toBeVisible();
});
