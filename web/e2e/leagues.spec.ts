// Chromatic's test captures a DOM snapshot at the end of every test (mock project only;
// see playwright.config.ts), through e2e/support/chromatic.ts, which keeps the bottom
// nav off a long page's content (#182). Otherwise identical to @playwright/test.
import { expect, test } from "./support/chromatic";
import type { Page } from "@playwright/test";
import { addAnotherPlayer, logIn, personaEmail, uniqueName } from "./support/auth";
import { expectNoSeriousA11yViolations } from "./support/axe";

/** On Manage: adds a Player to the first Club for an email nobody has signed in with yet. */
async function addClubPlayer(manager: Page, name: string, email: string) {
  await manager.goto("/manage");
  await manager.getByLabel("Name (optional)").first().fill(name);
  await manager.getByLabel("Their email address").first().fill(email);
  await manager.getByRole("button", { name: "Add", exact: true }).first().click();
  await expect(manager.getByRole("status")).toContainText(`${name} is added`);
}

/**
 * Call it last in a test that ends on the Leagues page. The page lists every
 * League of the Club as it stands when it loads, other tests' too, so its
 * snapshot would change with the suite's timing. Leaves the test's own League
 * showing, and "You can join" only if it is in there: the section is otherwise
 * empty or not by timing alone. A style rather than taking them out of the
 * page, which React could put back as it hydrates; it needs the page's nonce
 * (ADR-0027).
 */
async function showOnly(page: Page, league: string) {
  await page.evaluate((name) => {
    const style = document.createElement("style");
    style.nonce = document.querySelector<HTMLElement>("[nonce]")?.nonce ?? "";
    const card = `section[aria-label=${JSON.stringify(name)}]`;
    style.textContent = `
      main section[aria-label]:not(${card}) { display: none; }
      main section[aria-labelledby="joinable"]:not(:has(${card})) { display: none; }
    `;
    document.head.append(style);
  }, league);
  await expect(page.getByRole("main").locator("section[aria-label]").filter({ visible: true })).toHaveCount(1);
}

/**
 * From Manage: creates a League in the first Club with the setup form's
 * defaults (or another Type, other dates, or Stages of more months), and
 * lands on its page.
 *
 * A League a test starts should keep its Stage 1 deadline far off: once it
 * passes, the ladder shows the "held up" banner (#10), and the snapshot of
 * the page would change with the real date.
 */
async function createLeague(
  manager: Page,
  name: string,
  type?: string,
  { starts = "2026-12-01", ends = "2027-03-31", stageMonths }: { starts?: string; ends?: string; stageMonths?: number } = {},
) {
  await manager.goto("/manage");
  // The first Club listed: the Admin specs create others, which sort after it.
  await manager.getByRole("link", { name: "New league" }).first().click();
  await expect(manager.getByRole("heading", { name: "New league", level: 1 })).toBeVisible();
  await manager.getByLabel("Name", { exact: true }).fill(name);
  await manager.getByLabel("Starts").fill(starts);
  await manager.getByLabel("Ends").fill(ends);
  if (stageMonths) await manager.getByLabel("Stage length", { exact: true }).fill(String(stageMonths));
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
  await expect(manager.getByText("Divisional · Divisions of 4 · calendar month Stages · Manager closes")).toBeVisible();
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
  await showOnly(page, league);
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
  await showOnly(page, league);
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

  // Roll mode, and Teams per Division (ADR-0031), are a Divisional League's only.
  const divisionSize = page.getByLabel("Teams per Division");
  await expect(page.getByRole("group", { name: "When a Stage ends" })).toHaveCount(0);
  await expect(divisionSize).toHaveCount(0);
  await page.getByRole("radio", { name: "Divisional ladder" }).check({ force: true });
  await expect(page.getByRole("group", { name: "When a Stage ends" })).toBeVisible();
  await expect(divisionSize).toHaveValue("4");
  // Foursomes' Divisions are always 4 Players.
  await page.getByLabel("Type").selectOption("Foursomes");
  await expect(divisionSize).toHaveCount(0);
  await page.getByLabel("Type").selectOption("DoublesMix");

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
  await expect(page.getByRole("region", { name: league })).toHaveCount(0);
  await showOnly(page, renamed);
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
  await showOnly(page, league);
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
  await showOnly(page, league);
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
  await showOnly(page, league);
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
  await showOnly(page, league);
});

test("a Manager starts a League, reordering and leaving one out; each Player sees their Division and everyone they play", async ({
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
  await createLeague(manager, league, undefined, { starts: "2030-12-01", ends: "2031-03-31" });
  for (const { name, email } of players) {
    await manager.getByLabel("Player", { exact: true }).selectOption({ label: `${name} (${email})` });
    await manager.getByRole("button", { name: "Add to league" }).click();
    await expect(manager.getByRole("list", { name: "Players in the league" })).toContainText(name);
  }

  // Proposed in the order they joined, one Division of 4 (the League's size);
  // Xan moves above Wes, and Wes is left out: a Division of 3, no Bye (ADR-0031).
  await manager.getByRole("link", { name: "Start the league" }).click();
  await expect(manager.getByRole("heading", { name: `Start ${league}`, level: 1 })).toBeVisible();
  const division = manager.getByRole("list", { name: "Division 1" });
  await expect(division.getByRole("listitem")).toHaveText(players.map(({ name }) => new RegExp(name)));
  await manager.getByRole("button", { name: `Move ${xan.name} up` }).click();
  await expect(manager.getByRole("button", { name: `Move ${xan.name} up` })).toBeFocused();
  await manager.getByRole("button", { name: `Leave out ${wes.name}` }).click();
  await expect(division.getByRole("listitem")).toHaveText([una, vic, xan].map(({ name }) => new RegExp(name)));
  await expect(manager.getByRole("heading", { name: /Division 1 · 3 Teams · 3 Games/ })).toBeVisible();
  await expect(manager.getByRole("list", { name: "Left out" })).toContainText(wes.name);
  await expectNoSeriousA11yViolations(manager);
  await manager.getByRole("button", { name: "Confirm and start" }).click();

  await expect(manager.getByRole("status").filter({ hasText: "has started" })).toContainText(`${league} has started`);
  await expect(manager.getByText("Stage 1 runs 1 Dec 2030 – 31 Dec 2030.")).toBeVisible();
  const inLeague = manager.getByRole("list", { name: "Players in the league" });
  await expect(inLeague.getByRole("listitem").filter({ hasText: una.name })).toContainText("Division 1");
  await expect(inLeague.getByRole("listitem").filter({ hasText: wes.name })).toContainText("Not on the ladder");
  await expect(inLeague.getByRole("button", { name: /^Remove/ })).toHaveCount(1);
  await managerContext.close();

  await logIn(page, testInfo, una.email);
  await page.goto("/leagues");
  await expect(page.getByRole("region", { name: `${una.name}'s leagues` }).getByRole("region", { name: league })).toContainText(
    `Division 1 You play ${vic.name} and ${xan.name}, once each.`,
  );

  // The ladder: Una's Division marked, with everyone she plays and its three Games; Wes, left out, nowhere.
  await page.getByRole("link", { name: `Open the ladder of ${league}` }).click();
  await expect(page.getByRole("heading", { name: league, level: 1 })).toBeVisible();
  await expect(page.getByRole("status")).toContainText(`You're in Division 1. You play ${vic.name} and ${xan.name}`);
  const divisionOne = page.getByRole("region", { name: "Division 1" });
  await expect(divisionOne).toContainText("You");
  await expect(divisionOne.getByRole("list", { name: "Division 1 Games" }).getByRole("listitem")).toHaveText([
    new RegExp(`${una.name} v ${vic.name}`),
    new RegExp(`${una.name} v ${xan.name}`),
    new RegExp(`${vic.name} v ${xan.name}`),
  ]);
  await expect(page.getByRole("list", { name: "Ladder" })).not.toContainText(wes.name);
  await expectNoSeriousA11yViolations(page);

  // Another Player on Una's login, not in this League: switching to them says so, rather than "not on the ladder".
  const ladderPage = page.url();
  const child = uniqueName(testInfo, "Una's Child");
  await page.getByRole("navigation", { name: "Main" }).getByRole("link", { name: "Profiles" }).click();
  await addAnotherPlayer(page, child);
  await page.goto(ladderPage);
  await page.getByRole("button", { name: /switch player/ }).click();
  await page.getByRole("dialog", { name: "Play as" }).getByRole("button", { name: child }).click();
  await expect(page.getByTestId("acting-player")).toHaveText(child);
  await expect(page.getByRole("status")).toContainText(`${child} isn't in this league.`);
  await expect(page.getByText("isn't on the ladder")).toHaveCount(0);

  // Wes, left out, is in the League but off its ladder, and is told so.
  const wesContext = await browser.newContext();
  const wesPage = await wesContext.newPage();
  await logIn(wesPage, testInfo, wes.email);
  await wesPage.goto(ladderPage);
  await expect(wesPage.getByRole("status")).toContainText(`${wes.name} isn't on the ladder this Stage.`);
  await wesContext.close();

  const xanContext = await browser.newContext();
  const xanPage = await xanContext.newPage();
  await logIn(xanPage, testInfo, xan.email);
  await xanPage.goto("/leagues");
  await expect(xanPage.getByRole("region", { name: `${xan.name}'s leagues` }).getByRole("region", { name: league })).toContainText(
    `Division 1 You play ${una.name} and ${vic.name}, once each.`,
  );
  await xanContext.close();
});

test("a Player enters a Game's score set by set, and the League sees it on the Game and the ladder", async ({
  page,
  browser,
}, testInfo) => {
  // Three sign-ins and many round trips: over the default timeout on a busy real-API run.
  test.slow();
  const league = uniqueName(testInfo, "Scoring Ladder");
  const [una, vic] = ["Una Scores", "Vic Scores"].map((name) => ({
    name: uniqueName(testInfo, name),
    email: personaEmail(testInfo, `scores-${name.slice(0, 3).toLowerCase()}`),
  }));

  // A League that started on 1 November 2026, so its Games can have been played by now.
  const managerContext = await browser.newContext();
  const manager = await managerContext.newPage();
  await logIn(manager, testInfo, personaEmail(testInfo, "manager"));
  for (const { name, email } of [una, vic]) await addClubPlayer(manager, name, email);
  // Its Stage runs a year, so its deadline is far off (see createLeague).
  await createLeague(manager, league, undefined, { starts: "2026-11-01", ends: "2030-12-31", stageMonths: 12 });
  for (const { name, email } of [una, vic]) {
    await manager.getByLabel("Player", { exact: true }).selectOption({ label: `${name} (${email})` });
    await manager.getByRole("button", { name: "Add to league" }).click();
    await expect(manager.getByRole("list", { name: "Players in the league" })).toContainText(name);
  }
  await manager.getByRole("link", { name: "Start the league" }).click();
  await manager.getByRole("button", { name: "Confirm and start" }).click();
  await expect(manager.getByRole("status").filter({ hasText: "has started" })).toBeVisible();
  await managerContext.close();

  // Una lands on Home (#42): her Game, owing a score, and the deadline.
  await logIn(page, testInfo, una.email);
  const home = page.getByRole("region", { name: league, exact: true });
  const homeGames = home.getByRole("list", { name: `Your games in ${league}` });
  await expect(homeGames.getByRole("listitem")).toHaveText([new RegExp(`v ${vic.name}.*Score needed`)]);
  await expect(home).toContainText("Stage 1 · play by 31 Oct 2027");
  await expect(page.getByText("1 score needed")).toBeVisible();
  await expectNoSeriousA11yViolations(page);

  // She opens the Game from Home: no score yet, and she can enter it.
  await homeGames.getByRole("link", { name: new RegExp(`v ${vic.name}`) }).click();
  await expect(page.getByRole("heading", { name: `${una.name} v ${vic.name}`, level: 1 })).toBeVisible();
  await expect(page.getByRole("region", { name: "No score yet" })).toContainText("Anyone who plays in it can enter the score.");
  await page.getByRole("link", { name: "Enter score" }).click();

  // One set of 6: 6–3, tapped in, on today's date.
  await expect(page.getByRole("heading", { name: "Enter score", level: 1 })).toBeVisible();
  const save = page.getByRole("button", { name: "Save score" });
  await expect(save).toBeDisabled();
  const tap = async (side: string, times: number) => {
    const more = page.getByRole("group", { name: `Set, ${side}` }).getByRole("button", { name: "One more" });
    for (let i = 0; i < times; i++) await more.click();
  };
  await tap(una.name, 6);
  await tap(vic.name, 3);
  await expect(page.getByLabel("Score so far")).toHaveText("6–3");
  await expect(page.getByRole("status")).toHaveText(`${una.name} wins`);
  await expectNoSeriousA11yViolations(page);
  await save.click();

  // Saved: the Match screen shows it, with who entered it, and the ladder has it.
  await expect(page.getByRole("status").filter({ hasText: "Score saved" })).toBeVisible();
  const score = page.getByRole("region", { name: "Score" });
  await expect(score.getByRole("row").filter({ has: page.getByRole("rowheader") })).toHaveText([
    new RegExp(`${una.name}.*6`),
    new RegExp(`${vic.name}.*3`),
  ]);
  await expect(score).toContainText(`${una.name} won`);
  await expect(score).toContainText(`entered by ${una.name}`);
  await expect(score).toContainText("Played");
  await expectNoSeriousA11yViolations(page);
  await page.getByRole("link", { name: "Back to the ladder" }).click();
  const games = page.getByRole("list", { name: "Division 1 Games" });
  await expect(games.getByRole("listitem")).toHaveText([new RegExp(`${una.name} v ${vic.name}.*6–3.*Played`)]);
  // The Standings follow (#10): one Division, so nobody moves.
  const standings = page.getByRole("table", { name: "Division 1 standings" }).getByRole("row").filter({ has: page.getByRole("rowheader") });
  await expect(standings).toHaveText([new RegExp(`^1${una.name}110\\+3stays$`), new RegExp(`^2${vic.name}101-3stays$`)]);

  // Vic, the other side, sees the same score, and corrects it to 6–4: the history keeps both.
  const ladderUrl = page.url();
  const vicContext = await browser.newContext();
  const vicPage = await vicContext.newPage();
  await logIn(vicPage, testInfo, vic.email);
  // On Vic's Home it's from his side: a loss, his games first.
  await expect(
    vicPage.getByRole("region", { name: league, exact: true }).getByRole("list", { name: `Your games in ${league}` }).getByRole("listitem"),
  ).toHaveText([new RegExp(`v ${una.name}.*Lost 3–6.*Played`)]);
  await expect(vicPage.getByText("no scores owed")).toBeVisible();
  await vicPage.goto(ladderUrl);
  await vicPage.getByRole("list", { name: "Division 1 Games" }).getByRole("link").click();
  await expect(vicPage.getByRole("region", { name: "Score" })).toContainText(`entered by ${una.name}`);
  // Read once the Match screen is showing: straight after the click it can still be the ladder's.
  const matchUrl = vicPage.url();
  await expect(vicPage.getByRole("link", { name: "Enter score" })).toHaveCount(0);
  // Una opens the correction form too, before Vic saves his (ADR-0018).
  await page.goto(matchUrl);
  await page.getByRole("link", { name: "Correct the score" }).click();
  await expect(page.getByRole("heading", { name: "Correct the score", level: 1 })).toBeVisible();
  await vicPage.getByRole("link", { name: "Correct the score" }).click();
  await expect(vicPage.getByRole("heading", { name: "Correct the score", level: 1 })).toBeVisible();
  await expect(vicPage.getByLabel("Score so far")).toHaveText("6–3");
  await vicPage.getByRole("group", { name: `Set, ${vic.name}` }).getByRole("button", { name: "One more" }).click();
  await expect(vicPage.getByLabel("Score so far")).toHaveText("6–4");
  await vicPage.getByRole("button", { name: "Save correction" }).click();
  await expect(vicPage.getByRole("status").filter({ hasText: "Correction saved" })).toBeVisible();
  await expect(vicPage.getByRole("list", { name: "Changes to the score" }).getByRole("listitem")).toHaveText([
    new RegExp(`${vic.name} corrected it .*6–3 · ${una.name} won.*6–4 · ${una.name} won`),
    new RegExp(`${una.name} entered it`),
  ]);
  await expectNoSeriousA11yViolations(vicPage);

  // Una's form was opened before Vic's correction: saving it is refused, and the form reopens with Vic's score.
  await page.getByRole("group", { name: `Set, ${vic.name}` }).getByRole("button", { name: "One less" }).click();
  await expect(page.getByLabel("Score so far")).toHaveText("6–2");
  await page.getByRole("button", { name: "Save correction" }).click();
  await expect(page.getByRole("note")).toContainText("Someone changed this score while you had it open.");
  await expect(page.getByLabel("Score so far")).toHaveText("6–4");
  await expect(page.getByRole("button", { name: "Save correction" })).toBeDisabled();
  await expectNoSeriousA11yViolations(page);
  // The form shows today's date, so the page Chromatic snapshots ends on the ladder instead.
  await page.goto(ladderUrl);
  await expect(games.getByRole("listitem")).toHaveText([new RegExp(`${una.name} v ${vic.name}.*6–4.*Played`)]);

  // The Manager closes Stage 1 (#10): Vic can't change its score any more, the Manager can still override it.
  const leagueId = new URL(ladderUrl).pathname.split("/").pop();
  const managerAgain = await browser.newContext();
  const managerPage = await managerAgain.newPage();
  await logIn(managerPage, testInfo, personaEmail(testInfo, "manager"));
  await managerPage.goto(`/manage/leagues/${leagueId}`);
  await managerPage.getByRole("link", { name: "Close Stage 1" }).click();
  await expect(managerPage.getByRole("heading", { name: "Close Stage 1", level: 1 })).toBeVisible();
  await managerPage.getByRole("button", { name: "Close Stage 1 and open Stage 2" }).click();
  await expect(managerPage.getByRole("status").filter({ hasText: "Stage 1 is closed" })).toContainText("Stage 2 runs");

  await vicPage.goto(matchUrl);
  await expect(vicPage.getByRole("note")).toContainText("Stage 1 is closed. This score can't be changed any more.");
  await expect(vicPage.getByRole("link", { name: "Correct the score" })).toHaveCount(0);

  await managerPage.goto(matchUrl);
  await managerPage.getByRole("link", { name: "Override score (manager)" }).click();
  await expect(managerPage.getByRole("heading", { name: "Override the score", level: 1 })).toBeVisible();
  // 6–4 becomes 4–6: Vic's win.
  for (let i = 0; i < 2; i++) {
    await managerPage.getByRole("group", { name: `Set, ${una.name}` }).getByRole("button", { name: "One less" }).click();
    await managerPage.getByRole("group", { name: `Set, ${vic.name}` }).getByRole("button", { name: "One more" }).click();
  }
  await expect(managerPage.getByLabel("Score so far")).toHaveText("4–6");
  await managerPage.getByRole("button", { name: "Save override" }).click();
  await expect(managerPage.getByRole("status").filter({ hasText: "Override saved" })).toBeVisible();
  await expect(managerPage.getByRole("list", { name: "Changes to the score" }).getByRole("listitem").first()).toContainText("overrode it");
  await expect(managerPage.getByRole("region", { name: "Score" })).toContainText(`${vic.name} won`);
  await managerAgain.close();
  await vicContext.close();
});

test.describe("with a Player whose name is one long word", () => {
  // Not worth a visual review: the ladder's standings table still overflows with such a name.
  test.use({ disableAutoSnapshot: true });

  test("Home and the ladder's Divisions keep to a phone's width", async ({ page, browser }, testInfo) => {
    test.slow();
    const league = uniqueName(testInfo, "Narrow Ladder");
    const una = { name: uniqueName(testInfo, "Una Narrow"), email: personaEmail(testInfo, "narrow-una") };
    const long = {
      name: uniqueName(testInfo, "OpponentWithAnUnusuallyLongUnbrokenName"),
      email: personaEmail(testInfo, "narrow-long"),
    };

    const managerContext = await browser.newContext();
    const manager = await managerContext.newPage();
    await logIn(manager, testInfo, personaEmail(testInfo, "manager"));
    for (const { name, email } of [una, long]) await addClubPlayer(manager, name, email);
    // Its Stage runs a year, so its deadline is far off (see createLeague).
    await createLeague(manager, league, undefined, { starts: "2026-11-01", ends: "2030-12-31", stageMonths: 12 });
    for (const { name, email } of [una, long]) {
      await manager.getByLabel("Player", { exact: true }).selectOption({ label: `${name} (${email})` });
      await manager.getByRole("button", { name: "Add to league" }).click();
      await expect(manager.getByRole("list", { name: "Players in the league" })).toContainText(name);
    }
    await manager.getByRole("link", { name: "Start the league" }).click();
    await manager.getByRole("button", { name: "Confirm and start" }).click();
    await expect(manager.getByRole("status").filter({ hasText: "has started" })).toBeVisible();
    await managerContext.close();

    // Home: her Game's opponent wraps inside the card, so nothing is wider than the screen.
    await logIn(page, testInfo, una.email);
    const home = page.getByRole("region", { name: league, exact: true });
    await expect(home).toContainText(long.name);
    const overflow = await page.evaluate(
      () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
    );
    expect(overflow, "horizontal scroll on Home at 360px").toBeLessThanOrEqual(0);

    // The ladder: each Division's card is the column's width, not the longest word's.
    await home.getByRole("link", { name: "Ladder" }).click();
    await expect(page.getByRole("heading", { name: league, level: 1 })).toBeVisible();
    const division = (await page.locator('ol[aria-label="Ladder"] > li').first().boundingBox())!;
    const content = await page.getByRole("main").evaluate((main) => {
      const { paddingLeft, paddingRight } = getComputedStyle(main);
      return main.clientWidth - Number.parseFloat(paddingLeft) - Number.parseFloat(paddingRight);
    });
    expect(division.width).toBeLessThanOrEqual(content + 1);
  });
});

test("a Manager settles Games nobody scored from the to-do list: records one, marks one not played", async ({ page }, testInfo) => {
  test.slow();
  const league = uniqueName(testInfo, "Settling Ladder");
  const players = ["Ada Settles", "Ben Settles", "Cy Settles"].map((name) => ({
    name: uniqueName(testInfo, name),
    email: personaEmail(testInfo, `settles-${name.split(" ")[0].toLowerCase()}`),
  }));
  const [ada, ben, cy] = players;

  await logIn(page, testInfo, personaEmail(testInfo, "manager"));
  for (const { name, email } of players) await addClubPlayer(page, name, email);
  // Its Stage runs a year, so its deadline is far off (see createLeague).
  await createLeague(page, league, undefined, { starts: "2026-11-01", ends: "2030-12-31", stageMonths: 12 });
  for (const { name, email } of players) {
    await page.getByLabel("Player", { exact: true }).selectOption({ label: `${name} (${email})` });
    await page.getByRole("button", { name: "Add to league" }).click();
    await expect(page.getByRole("list", { name: "Players in the league" })).toContainText(name);
  }
  await page.getByRole("link", { name: "Start the league" }).click();
  await page.getByRole("button", { name: "Confirm and start" }).click();
  await expect(page.getByRole("status").filter({ hasText: "has started" })).toBeVisible();
  const leaguePage = page.url();

  // Three Games, none scored: all on the to-do list.
  const toDo = page.getByRole("region", { name: "To do" }).getByRole("list", { name: "Games with no score" });
  await expect(toDo.getByRole("listitem")).toHaveText([
    new RegExp(`${ada.name} v ${ben.name}`),
    new RegExp(`${ada.name} v ${cy.name}`),
    new RegExp(`${ben.name} v ${cy.name}`),
  ]);
  await expectNoSeriousA11yViolations(page);

  // Ada v Ben: the Manager records it.
  await toDo.getByRole("listitem").first().getByRole("link", { name: "Settle" }).click();
  await page.getByRole("link", { name: "Record the score (manager)" }).click();
  await expect(page.getByRole("heading", { name: "Record the score", level: 1 })).toBeVisible();
  const more = page.getByRole("group", { name: `Set, ${ada.name}` }).getByRole("button", { name: "One more" });
  for (let i = 0; i < 6; i++) await more.click();
  await page.getByRole("button", { name: "Save score" }).click();
  await expect(page.getByRole("status").filter({ hasText: "Score recorded" })).toBeVisible();
  await expect(page.getByRole("region", { name: "Score" })).toContainText("recorded by a manager");

  // Ada v Cy: marked not played, undone, and marked again.
  await page.goto(leaguePage);
  await toDo.getByRole("listitem").filter({ hasText: cy.name }).first().getByRole("link", { name: "Settle" }).click();
  await page.getByRole("button", { name: "Mark not played (manager)" }).click();
  await expect(page.getByRole("region", { name: "Not played" })).toBeVisible();
  await page.getByRole("button", { name: "Undo: it can still be played" }).click();
  await expect(page.getByRole("region", { name: "No score yet" })).toBeVisible();
  await page.getByRole("button", { name: "Mark not played (manager)" }).click();
  await expect(page.getByRole("region", { name: "Not played" })).toBeVisible();

  // The ladder shows both; only Ben v Cy is left to settle.
  await page.getByRole("link", { name: "Back to the ladder" }).click();
  await expect(page.getByRole("list", { name: "Division 1 Games" }).getByRole("listitem")).toHaveText([
    new RegExp(`${ada.name} v ${ben.name}.*6–0.*Played`),
    new RegExp(`${ada.name} v ${cy.name}.*Void`),
    new RegExp(`${ben.name} v ${cy.name}.*Score needed`),
  ]);
  await page.goto(leaguePage);
  await expect(toDo.getByRole("listitem")).toHaveText([new RegExp(`${ben.name} v ${cy.name}`)]);
});

test("a Manager closes a Stage: the top goes up, the bottom down, a tie they order, a Team waiting joins, and Stage 2 opens; a score changed meanwhile is caught", async ({
  page,
}, testInfo) => {
  test.slow();
  const league = uniqueName(testInfo, "Closing Ladder");
  const players = ["Abe Closes", "Bo Closes", "Cat Closes", "Di Closes", "Ed Closes"].map((name) => ({
    name: uniqueName(testInfo, name),
    email: personaEmail(testInfo, `closes-${name.split(" ")[0].toLowerCase()}`),
  }));
  const [abe, bo, cat, di, ed] = players;

  await logIn(page, testInfo, personaEmail(testInfo, "manager"));
  for (const { name, email } of players) await addClubPlayer(page, name, email);
  // Its Stages run a year, so Stage 1's deadline is far off (see createLeague).
  await createLeague(page, league, undefined, { starts: "2026-11-01", ends: "2030-12-31", stageMonths: 12 });
  for (const { name, email } of players) {
    await page.getByLabel("Player", { exact: true }).selectOption({ label: `${name} (${email})` });
    await page.getByRole("button", { name: "Add to league" }).click();
    await expect(page.getByRole("list", { name: "Players in the league" })).toContainText(name);
  }
  // Ed waits off the ladder; the rest play in two Divisions of two.
  await page.getByRole("link", { name: "Start the league" }).click();
  await page.getByRole("button", { name: `Leave out ${ed.name}` }).click();
  await page.getByRole("button", { name: "More Divisions" }).click();
  await expect(page.getByRole("list", { name: "Division 2" }).getByRole("listitem")).toHaveText([new RegExp(cat.name), new RegExp(di.name)]);
  await page.getByRole("button", { name: "Confirm and start" }).click();
  await expect(page.getByRole("status").filter({ hasText: "has started" })).toBeVisible();
  const leaguePage = page.url();

  // Abe beats Bo; Cat v Di isn't played, so who goes up from Division 2 is the Manager's to order.
  const toDo = page.getByRole("region", { name: "To do" });
  await toDo.getByRole("listitem").filter({ hasText: `${abe.name} v ${bo.name}` }).getByRole("link", { name: "Settle" }).click();
  await page.getByRole("link", { name: "Record the score (manager)" }).click();
  const more = page.getByRole("group", { name: `Set, ${abe.name}` }).getByRole("button", { name: "One more" });
  for (let i = 0; i < 6; i++) await more.click();
  await page.getByRole("button", { name: "Save score" }).click();
  await expect(page.getByRole("status").filter({ hasText: "Score recorded" })).toBeVisible();
  await page.goto(leaguePage);
  await toDo.getByRole("listitem").filter({ hasText: `${cat.name} v ${di.name}` }).getByRole("link", { name: "Settle" }).click();
  await page.getByRole("button", { name: "Mark not played (manager)" }).click();
  await expect(page.getByRole("region", { name: "Not played" })).toBeVisible();
  await page.goto(leaguePage);
  await expect(toDo.getByRole("list", { name: "Ties that decide who moves" })).toContainText(`Division 2: ${cat.name}, ${di.name}`);

  // The close screen: Abe stays, Bo goes down; the Manager puts Di above Cat, and ticks Ed.
  await toDo.getByRole("link", { name: "Close Stage 1" }).click();
  await expect(page.getByRole("heading", { name: "Close Stage 1", level: 1 })).toBeVisible();
  const divisionOne = page.getByRole("list", { name: "Division 1 order" }).getByRole("listitem");
  const divisionTwo = page.getByRole("list", { name: "Division 2 order" }).getByRole("listitem");
  await expect(divisionOne).toHaveText([new RegExp(`${abe.name}.*stays`), new RegExp(`${bo.name}.*▼ to Division 2`)]);
  const orderAndTick = async () => {
    await page.getByRole("button", { name: `Move ${di.name} up` }).click();
    await expect(divisionTwo).toHaveText([new RegExp(`${di.name}.*▲ to Division 1`), new RegExp(`${cat.name}.*stays`)]);
    await page.getByRole("checkbox", { name: ed.name }).check();
  };
  await orderAndTick();
  // Stage 2 a week later than proposed.
  await page.getByLabel("Starts").fill("2027-11-08");
  await page.getByLabel("Ends").fill("2028-11-07");
  await expectNoSeriousA11yViolations(page);

  // Meanwhile Abe v Bo is overridden, in another tab: Bo won it after all.
  const otherTab = await page.context().newPage();
  await otherTab.goto(leaguePage);
  await otherTab.getByRole("link", { name: "See the ladder" }).click();
  await otherTab.getByRole("list", { name: "Division 1 Games" }).getByRole("link").click();
  await otherTab.getByRole("link", { name: "Override score (manager)" }).click();
  const abeLess = otherTab.getByRole("group", { name: `Set, ${abe.name}` }).getByRole("button", { name: "One less" });
  const boMore = otherTab.getByRole("group", { name: `Set, ${bo.name}` }).getByRole("button", { name: "One more" });
  for (let i = 0; i < 6; i++) await abeLess.click();
  for (let i = 0; i < 6; i++) await boMore.click();
  await expect(otherTab.getByLabel("Score so far")).toHaveText("0–6");
  await otherTab.getByRole("button", { name: "Save override" }).click();
  await expect(otherTab.getByRole("status").filter({ hasText: "Override saved" })).toBeVisible();
  await otherTab.close();

  // Closing on what the screen showed is refused; it reloads as things now stand, the order and ticks afresh.
  await page.getByRole("button", { name: "Close Stage 1 and open Stage 2" }).click();
  await expect(page.getByRole("alert").filter({ hasText: "Things changed while you were looking" })).toBeVisible();
  await expect(divisionOne).toHaveText([new RegExp(`${bo.name}.*stays`), new RegExp(`${abe.name}.*▼ to Division 2`)]);
  await expect(page.getByRole("checkbox", { name: ed.name })).not.toBeChecked();
  // The dates don't depend on the scores: they stay as typed (#217 review).
  await expect(page.getByLabel("Starts")).toHaveValue("2027-11-08");
  await expect(page.getByLabel("Ends")).toHaveValue("2028-11-07");
  await orderAndTick();
  // The Manager moves Stage 2's start back into Stage 1 by mistake: the API refuses it, and both dates keep
  // what they typed.
  await page.getByLabel("Starts").fill("2027-10-31");
  await page.getByRole("button", { name: "Close Stage 1 and open Stage 2" }).click();
  await expect(page.getByText(/The next Stage starts after this one ends/)).toBeVisible();
  await expect(page.getByLabel("Starts")).toHaveValue("2027-10-31");
  await expect(page.getByLabel("Ends")).toHaveValue("2028-11-07");
  await page.getByLabel("Starts").fill("2027-11-08");
  await page.getByRole("button", { name: "Close Stage 1 and open Stage 2" }).click();
  await expect(page.getByRole("status").filter({ hasText: "Stage 1 is closed" })).toContainText(
    "Stage 1 is closed, and Stage 2 runs 8 Nov 2027 – 7 Nov 2028.",
  );

  // Stage 2's ladder: Bo and Di; then Abe, Cat and Ed, each pair with a Game to play.
  await page.getByRole("link", { name: "See the ladder" }).click();
  const standings = (n: number) =>
    page.getByRole("table", { name: `Division ${n} standings` }).getByRole("row").filter({ has: page.getByRole("rowheader") });
  await expect(standings(1)).toHaveText([new RegExp(bo.name), new RegExp(di.name)]);
  await expect(standings(2)).toHaveText([new RegExp(abe.name), new RegExp(cat.name), new RegExp(ed.name)]);
  await expect(page.getByRole("list", { name: "Division 2 Games" }).getByRole("listitem")).toHaveCount(3);
});

test("a Manager moves a Team to another Division mid-Stage, and can't leave a Division with one", async ({ page }, testInfo) => {
  test.slow();
  const league = uniqueName(testInfo, "Moving Ladder");
  const players = ["Al Moves", "Bea Moves", "Cy Moves", "Di Moves", "Em Moves"].map((name) => ({
    name: uniqueName(testInfo, name),
    email: personaEmail(testInfo, `moves-${name.split(" ")[0].toLowerCase()}`),
  }));
  const [al, bea, cy, di, em] = players;

  await logIn(page, testInfo, personaEmail(testInfo, "manager"));
  for (const { name, email } of players) await addClubPlayer(page, name, email);
  // Its Stage runs a year, so its deadline is far off (see createLeague).
  await createLeague(page, league, undefined, { starts: "2026-11-01", ends: "2030-12-31", stageMonths: 12 });
  for (const { name, email } of players) {
    await page.getByLabel("Player", { exact: true }).selectOption({ label: `${name} (${email})` });
    await page.getByRole("button", { name: "Add to league" }).click();
    await expect(page.getByRole("list", { name: "Players in the league" })).toContainText(name);
  }
  // Two Divisions: Al and Bea, then Cy, Di and Em.
  await page.getByRole("link", { name: "Start the league" }).click();
  await page.getByRole("button", { name: "More Divisions" }).click();
  await expect(page.getByRole("list", { name: "Division 1" }).getByRole("listitem")).toHaveCount(2);
  await page.getByRole("button", { name: "Confirm and start" }).click();
  await expect(page.getByRole("status").filter({ hasText: "has started" })).toBeVisible();
  const leaguePage = page.url();

  // Di v Em isn't going to be played.
  await page.getByRole("link", { name: "See the ladder" }).click();
  await page.getByRole("list", { name: "Division 2 Games" }).getByRole("link", { name: new RegExp(`${di.name} v ${em.name}`) }).click();
  await page.getByRole("button", { name: "Mark not played (manager)" }).click();
  await expect(page.getByRole("region", { name: "Not played" })).toBeVisible();

  // Em moves up: the bottom of Division 1, playing Al and Bea.
  await page.goto(leaguePage);
  const move = page.getByRole("region", { name: "Move a Team" });
  const moveTo = async (team: string, from: number, to: number) => {
    await move.getByLabel("Team").selectOption({ label: `${team} · Division ${from}` });
    await move.getByLabel("To Division").selectOption({ label: `Division ${to}` });
    await move.getByRole("button", { name: "Move" }).click();
  };
  await moveTo(em.name, 2, 1);
  await expect(move.getByRole("status")).toContainText(`${em.name} is in Division 1 now`);
  await expectNoSeriousA11yViolations(page);

  // Division 2 has two left: Di can't leave it.
  await moveTo(di.name, 2, 1);
  await expect(move.getByRole("alert")).toContainText("That would leave its Division with one Team.");

  // Em goes back: a new Game against Di, waiting for a score, not the one marked not played (#219 review).
  await moveTo(em.name, 1, 2);
  await expect(move.getByRole("status")).toContainText(`${em.name} is in Division 2 now`);
  await page.getByRole("link", { name: "See the ladder" }).click();
  await expect(page.getByRole("list", { name: "Division 1 Games" }).getByRole("listitem")).toHaveText([
    new RegExp(`${al.name} v ${bea.name}`),
  ]);
  await expect(page.getByRole("list", { name: "Division 2 Games" }).getByRole("listitem")).toHaveText([
    new RegExp(`${cy.name} v ${di.name}.*Score needed`),
    new RegExp(`${cy.name} v ${em.name}.*Score needed`),
    new RegExp(`${di.name} v ${em.name}.*Score needed`),
  ]);
});

test("a Player can't open a Manager's League pages", async ({ page }, testInfo) => {
  await logIn(page, testInfo, personaEmail(testInfo, "player"));
  await page.goto("/manage/leagues/new");
  await expect(page.getByRole("heading", { name: "Page not found" })).toBeVisible();
});
