// Chromatic's test captures a DOM snapshot at the end of every test (mock project only;
// see playwright.config.ts), through e2e/support/chromatic.ts, which keeps the bottom
// nav off a long page's content (#182). Otherwise identical to @playwright/test.
import { expect, test } from "./support/chromatic";
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

/** From Manage: creates a League in the first Club with the setup form's defaults (or another Type), and lands on its page. */
async function createLeague(manager: Page, name: string, type?: string) {
  await manager.goto("/manage");
  // The first Club listed: the Admin specs create others, which sort after it.
  await manager.getByRole("link", { name: "New league" }).first().click();
  await expect(manager.getByRole("heading", { name: "New league", level: 1 })).toBeVisible();
  await manager.getByLabel("Name", { exact: true }).fill(name);
  await manager.getByLabel("Starts").fill("2026-12-01");
  await manager.getByLabel("Ends").fill("2027-03-31");
  if (type) await manager.getByLabel("Type").selectOption(type);
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

  // The Manager sees it waiting, and approves it. No Rating on requests or players (#161).
  await manager.reload();
  await expect(manager.getByText(/\brating\b/i)).toHaveCount(0);
  await manager.getByRole("button", { name: `Approve ${robin}` }).click();
  await expect(manager.getByRole("button", { name: `Approve ${robin}` })).toHaveCount(0);
  const players = manager.getByRole("list", { name: "Players in the league" });
  await expect(players).toContainText(robin);
  // A participant's Rating was a bare number beside "Remove": no element in the row is only digits.
  await expect(players.getByRole("listitem").filter({ hasText: robin }).getByText(/^\d+$/)).toHaveCount(0);
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

test("a Manager pairs two Players of a doubles League into a team, and each sees their partner", async ({ page, browser }, testInfo) => {
  // Two sign-ins and several round trips: over the default timeout on a busy real-API run.
  test.slow();
  const league = uniqueName(testInfo, "Pairs Ladder");
  const ana = uniqueName(testInfo, "Ana Pair");
  const ben = uniqueName(testInfo, "Ben Pair");
  const cal = uniqueName(testInfo, "Cal Pair");
  const dee = uniqueName(testInfo, "Dee Pair");
  const anaEmail = personaEmail(testInfo, "pair-ana");
  const benEmail = personaEmail(testInfo, "pair-ben");
  const calEmail = personaEmail(testInfo, "pair-cal");
  const deeEmail = personaEmail(testInfo, "pair-dee");

  const managerContext = await browser.newContext();
  const manager = await managerContext.newPage();
  await logIn(manager, testInfo, personaEmail(testInfo, "manager"));
  const players = [
    [ana, anaEmail],
    [ben, benEmail],
    [cal, calEmail],
    [dee, deeEmail],
  ];
  for (const [name, email] of players) await addClubPlayer(manager, name, email);
  await createLeague(manager, league, "DoublesMix");
  for (const [name, email] of players) {
    await manager.getByLabel("Player", { exact: true }).selectOption({ label: `${name} (${email})` });
    await manager.getByRole("button", { name: "Add to league" }).click();
    await expect(manager.getByRole("list", { name: "Players in the league" })).toContainText(name);
  }
  const unpaired = manager.getByRole("list", { name: "Players without a partner" });
  await expect(unpaired.getByRole("listitem")).toHaveCount(4);

  const pair = async (first: string, second: string) => {
    await manager.getByLabel("First player").selectOption({ label: first });
    await manager.getByLabel("Second player").selectOption({ label: second });
    await manager.getByRole("button", { name: "Pair as a team" }).click();
    await expect(manager.getByRole("list", { name: "Teams in the league" })).toContainText(`${first} & ${second}`);
  };
  await pair(ana, ben);
  // Two are still without a partner, and the form starts again empty for them.
  await expect(unpaired.getByRole("listitem")).toHaveCount(2);
  await expect(manager.getByLabel("First player")).toHaveValue("");
  await expect(manager.getByLabel("Second player")).toHaveValue("");
  await pair(cal, dee);
  await expect(manager.getByText("Everyone in the league has a partner.")).toBeVisible();
  await expectNoSeriousA11yViolations(manager);

  // Unpairing puts both back without a partner; then they are paired again.
  await manager.getByRole("button", { name: `Unpair ${ana} & ${ben}` }).click();
  await expect(unpaired.getByRole("listitem")).toHaveCount(2);
  await pair(ana, ben);
  await managerContext.close();

  await logIn(page, testInfo, anaEmail);
  await page.goto("/leagues");
  const mine = page.getByRole("region", { name: `${ana}'s leagues` });
  await expect(mine.getByRole("region", { name: league })).toContainText(`Partner: ${ben}`);
});

test("a Player asks to join a doubles League with a partner, who can take it back; approved, they're a team", async ({
  page,
  browser,
}, testInfo) => {
  // Three sign-ins and several round trips: over the default timeout on a busy real-API run.
  test.slow();
  const league = uniqueName(testInfo, "Partners Ladder");
  const eve = uniqueName(testInfo, "Eve Asks");
  const fin = uniqueName(testInfo, "Fin Named");
  const eveEmail = personaEmail(testInfo, "asks-eve");
  const finEmail = personaEmail(testInfo, "named-fin");

  const managerContext = await browser.newContext();
  const manager = await managerContext.newPage();
  await logIn(manager, testInfo, personaEmail(testInfo, "manager"));
  await addClubPlayer(manager, eve, eveEmail);
  await addClubPlayer(manager, fin, finEmail);
  await createLeague(manager, league, "DoublesMix");
  const leaguePage = manager.url();

  // Eve picks Fin by name: typing part of it, then choosing from the list.
  await logIn(page, testInfo, eveEmail);
  await page.goto("/leagues");
  const evesCard = page.getByRole("region", { name: "You can join" }).getByRole("region", { name: league });
  const partner = evesCard.getByRole("combobox", { name: "Partner (optional)" });
  const askWithFin = async () => {
    await partner.pressSequentially(fin.slice(0, 9));
    await expect(partner).toHaveAttribute("aria-expanded", "true");
    await evesCard.getByRole("option", { name: fin }).click();
    await expect(partner).toHaveValue(fin);
    await evesCard.getByRole("button", { name: `Ask to join ${league}` }).click();
    await expect(evesCard).toContainText(`Asked with ${fin}`);
  };
  await partner.pressSequentially(fin.slice(0, 9));
  await expectNoSeriousA11yViolations(page);
  await partner.clear();
  await askWithFin();

  // Fin sees it, and takes it back: they hadn't agreed after all.
  const finContext = await browser.newContext();
  const finPage = await finContext.newPage();
  await logIn(finPage, testInfo, finEmail);
  await finPage.goto("/leagues");
  const finsCard = finPage.getByRole("region", { name: "You can join" }).getByRole("region", { name: league });
  await expect(finsCard).toContainText(`${eve} asked to join with you`);
  await expectNoSeriousA11yViolations(finPage);
  await finsCard.getByRole("button", { name: `Take back ${eve}'s request to join ${league} with you` }).click();
  await expect(finsCard.getByRole("button", { name: `Ask to join ${league}` })).toBeVisible();

  // Eve may ask again; this time they have agreed, and the Manager approves the pair.
  await page.reload();
  await askWithFin();
  await manager.goto(leaguePage);
  await manager.getByRole("button", { name: `Approve ${eve} with ${fin}` }).click();
  // Both joined at the request time, so either may be listed first.
  await expect(manager.getByRole("list", { name: "Teams in the league" })).toContainText(
    new RegExp(`${eve} & ${fin}|${fin} & ${eve}`),
  );
  await managerContext.close();

  await finPage.reload();
  await expect(finPage.getByRole("region", { name: `${fin}'s leagues` }).getByRole("region", { name: league })).toContainText(
    `Partner: ${eve}`,
  );
  await finContext.close();
  await page.reload();
  await expect(page.getByRole("region", { name: `${eve}'s leagues` }).getByRole("region", { name: league })).toContainText(
    `Partner: ${fin}`,
  );
});

test("a named partner sees the request wherever they are, and the Manager is told when the partner is the problem", async ({
  page,
  browser,
}, testInfo) => {
  // Four sign-ins and many round trips: over the default timeout on a busy real-API run.
  test.slow();
  const league = uniqueName(testInfo, "Partner Cases");
  const hal = uniqueName(testInfo, "Hal Asks");
  const gus = uniqueName(testInfo, "Gus Already In");
  const kai = uniqueName(testInfo, "Kai Already In");
  const ivy = uniqueName(testInfo, "Ivy Turned Down");
  const halEmail = personaEmail(testInfo, "cases-hal");
  const gusEmail = personaEmail(testInfo, "cases-gus");
  const ivyEmail = personaEmail(testInfo, "cases-ivy");
  const kaiEmail = personaEmail(testInfo, "cases-kai");

  const managerContext = await browser.newContext();
  const manager = await managerContext.newPage();
  await logIn(manager, testInfo, personaEmail(testInfo, "manager"));
  for (const [name, email] of [
    [hal, halEmail],
    [gus, gusEmail],
    [kai, kaiEmail],
    [ivy, ivyEmail],
  ])
    await addClubPlayer(manager, name, email);
  await createLeague(manager, league, "DoublesMix");
  const leaguePage = manager.url();
  for (const [name, email] of [
    [gus, gusEmail],
    [kai, kaiEmail],
  ]) {
    await manager.getByLabel("Player", { exact: true }).selectOption({ label: `${name} (${email})` });
    await manager.getByRole("button", { name: "Add to league" }).click();
    await expect(manager.getByRole("list", { name: "Players in the league" })).toContainText(name);
  }

  // Ivy asks alone and is turned down.
  const ivyContext = await browser.newContext();
  const ivyPage = await ivyContext.newPage();
  await logIn(ivyPage, testInfo, ivyEmail);
  await ivyPage.goto("/leagues");
  const ivysCard = ivyPage.getByRole("region", { name: "You can join" }).getByRole("region", { name: league });
  await ivysCard.getByRole("button", { name: `Ask to join ${league}` }).click();
  await expect(ivysCard).toContainText("Request sent");
  await manager.goto(leaguePage);
  await manager.getByRole("button", { name: `Reject ${ivy}` }).click();
  await expect(manager.getByRole("list", { name: "Rejected requests" })).toContainText(ivy);

  // Hal asks with Gus, who is already in the League without a partner.
  await logIn(page, testInfo, halEmail);
  await page.goto("/leagues");
  const halsCard = page.getByRole("region", { name: "You can join" }).getByRole("region", { name: league });
  const askWith = async (partner: string) => {
    await halsCard.getByRole("combobox", { name: "Partner (optional)" }).pressSequentially(partner.slice(0, 9));
    await halsCard.getByRole("option", { name: partner }).click();
    await halsCard.getByRole("button", { name: `Ask to join ${league}` }).click();
    await expect(halsCard).toContainText(`Asked with ${partner}`);
  };
  await askWith(gus);

  // Gus sees it on his own League card, and can take it back.
  const gusContext = await browser.newContext();
  const gusPage = await gusContext.newPage();
  await logIn(gusPage, testInfo, gusEmail);
  await gusPage.goto("/leagues");
  const gusCard = gusPage.getByRole("region", { name: `${gus}'s leagues` }).getByRole("region", { name: league });
  await expect.soft(gusCard).toContainText(`${hal} asked to join with you`);
  await expect
    .soft(gusCard.getByRole("button", { name: `Take back ${hal}'s request to join ${league} with you` }))
    .toBeVisible();
  await gusContext.close();

  // Gus is paired with Kai meanwhile: approving Hal's request says the partner is why.
  await manager.goto(leaguePage);
  await manager.getByLabel("First player").selectOption({ label: gus });
  await manager.getByLabel("Second player").selectOption({ label: kai });
  await manager.getByRole("button", { name: "Pair as a team" }).click();
  await expect(manager.getByRole("list", { name: "Teams in the league" })).toContainText(gus);
  await manager.getByRole("button", { name: `Approve ${hal} with ${gus}` }).click();
  await expect.soft(manager.getByText("One of them already has a partner in this league.")).toBeVisible();
  await managerContext.close();

  // Hal takes it back and asks with Ivy, whose own request was turned down: she sees it too.
  await page.getByRole("button", { name: `Take back the request to join ${league}` }).click();
  await askWith(ivy);
  await ivyPage.reload();
  await expect(ivysCard).toContainText("Not accepted");
  await expect.soft(ivysCard).toContainText(`${hal} asked to join with you`);
  await ivyContext.close();
  // Taken back again, so the snapshot of this page carries no ask date.
  await page.getByRole("button", { name: `Take back the request to join ${league}` }).click();
  await expect(halsCard.getByRole("button", { name: `Ask to join ${league}` })).toBeVisible();

  // A made-up error code shows nothing, rather than breaking the page.
  await page.goto("/leagues?error=toString");
  await expect.soft(page.getByRole("heading", { name: "Leagues", level: 1 })).toBeVisible();
});

test("a Manager starts a League, reordering and leaving one out; each Player sees their Division and opponent, or the Bye", async ({
  page,
  browser,
}, testInfo) => {
  // Three sign-ins and many round trips: over the default timeout on a busy real-API run.
  test.slow();
  const league = uniqueName(testInfo, "Starting Ladder");
  const players = ["Una Starts", "Vic Starts", "Wes Starts", "Xan Starts"].map((name) => ({
    name: uniqueName(testInfo, name),
    email: personaEmail(testInfo, `starts-${name.slice(0, 3).toLowerCase()}`),
  }));
  const [una, vic, wes, xan] = players;

  const managerContext = await browser.newContext();
  const manager = await managerContext.newPage();
  await logIn(manager, testInfo, personaEmail(testInfo, "manager"));
  for (const { name, email } of players) await addClubPlayer(manager, name, email);
  await createLeague(manager, league);
  for (const { name, email } of players) {
    await manager.getByLabel("Player", { exact: true }).selectOption({ label: `${name} (${email})` });
    await manager.getByRole("button", { name: "Add to league" }).click();
    await expect(manager.getByRole("list", { name: "Players in the league" })).toContainText(name);
  }

  // Proposed in the order they joined; Xan moves above Wes, and Wes is left out.
  await manager.getByRole("link", { name: "Start the league" }).click();
  await expect(manager.getByRole("heading", { name: `Start ${league}`, level: 1 })).toBeVisible();
  const ladder = manager.getByRole("list", { name: "Starting ladder" });
  await expect(ladder.getByRole("listitem")).toHaveText(players.map(({ name }) => new RegExp(name)));
  await manager.getByRole("button", { name: `Move ${xan.name} up` }).click();
  await expect(manager.getByRole("button", { name: `Move ${xan.name} up` })).toBeFocused();
  await manager.getByRole("button", { name: `Leave out ${wes.name}` }).click();
  await expect(ladder.getByRole("listitem").filter({ hasText: xan.name })).toContainText("Bye");
  await expect(manager.getByText(`${xan.name} has the Bye`)).toBeVisible();
  await expectNoSeriousA11yViolations(manager);
  await manager.getByRole("button", { name: "Confirm and start" }).click();

  await expect(manager.getByRole("status").filter({ hasText: "has started" })).toContainText(`${league} has started`);
  await expect(manager.getByText("Stage 1 runs 1 Dec 2026 – 31 Dec 2026.")).toBeVisible();
  const inLeague = manager.getByRole("list", { name: "Players in the league" });
  await expect(inLeague.getByRole("listitem").filter({ hasText: una.name })).toContainText("Division 1");
  await expect(inLeague.getByRole("listitem").filter({ hasText: wes.name })).toContainText("Not on the ladder");
  await expect(inLeague.getByRole("button", { name: /^Remove/ })).toHaveCount(1);
  await managerContext.close();

  await logIn(page, testInfo, una.email);
  await page.goto("/leagues");
  await expect(page.getByRole("region", { name: `${una.name}'s leagues` }).getByRole("region", { name: league })).toContainText(
    `Division 1 You play ${vic.name}.`,
  );

  const xanContext = await browser.newContext();
  const xanPage = await xanContext.newPage();
  await logIn(xanPage, testInfo, xan.email);
  await xanPage.goto("/leagues");
  await expect(xanPage.getByRole("region", { name: `${xan.name}'s leagues` }).getByRole("region", { name: league })).toContainText(
    "You sit out this Stage",
  );
  await xanContext.close();
});

test("a Player can't open a Manager's League pages", async ({ page }, testInfo) => {
  await logIn(page, testInfo, personaEmail(testInfo, "player"));
  await page.goto("/manage/leagues/new");
  await expect(page.getByRole("heading", { name: "Page not found" })).toBeVisible();
});
