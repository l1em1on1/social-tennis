// Chromatic's test captures a DOM snapshot at the end of every test (mock project only;
// see playwright.config.ts), through e2e/support/chromatic.ts. Otherwise identical to
// @playwright/test.
import { expect, test } from "./support/chromatic";
import type { Page } from "@playwright/test";
import { addFirstPlayer, asAdmin, logIn, personaEmail, uniqueName, userIdOf } from "./support/auth";
import { expectNoSeriousA11yViolations } from "./support/axe";
import { addClubPlayer, createLeague } from "./support/league";

// Manage lists the whole suite's members, and these tests end on pages of their own data:
// the fixed world's snapshots (manager-pages.spec.ts) are what's reviewed.
test.use({ disableAutoSnapshot: true });

const mainNav = (page: Page) => page.getByRole("navigation", { name: "Main" });
const pathOf = (page: Page) => new URL(page.url()).pathname;
// Their Stages are far off, so nothing here depends on today's date.
const dates = { starts: "2030-12-01", ends: "2031-03-31" };

test("a Club's manager makes a member a League manager, who creates a League and runs that alone", async ({
  page,
  browser,
}, testInfo) => {
  // Four sign-ins' worth of round trips: over the default timeout on a busy real-API run.
  test.slow();
  const email = personaEmail(testInfo, "runner");
  const name = uniqueName(testInfo, "Robin Runner");
  const own = uniqueName(testInfo, "Robin's League");
  const clubs = uniqueName(testInfo, "The Club's League");

  const manager = await (await browser.newContext()).newPage();
  await logIn(manager, testInfo, personaEmail(testInfo, "manager"));
  await addClubPlayer(manager, name, email);
  await createLeague(manager, clubs, undefined, dates);
  const clubsPath = pathOf(manager);

  // In the Club, and nothing to manage.
  await logIn(page, testInfo, email);
  await expect(page.getByTestId("acting-player")).toHaveText(name);
  await expect(mainNav(page).getByRole("link")).toHaveText(["Home", "Leagues", "Profiles"]);

  await manager.goto("/manage");
  await manager.getByRole("button", { name: `Role for ${email}` }).click();
  const sheet = manager.getByRole("dialog", { name: `Role for ${name}` });
  await expect(sheet).toContainText("They don't manage the club or its members.");
  await expectNoSeriousA11yViolations(manager);
  await sheet.getByRole("button", { name: /^Make league manager/ }).click();
  await expect(manager.getByRole("button", { name: `Remove ${email} as league manager` })).toBeVisible();

  // Takes effect on their next page: the Club's Leagues they run, and a way to a new one. No members, no join link.
  await page.reload();
  await expect(mainNav(page).getByRole("link")).toHaveText(["Home", "Leagues", "Profiles", "Manage"]);
  await mainNav(page).getByRole("link", { name: "Manage" }).click();
  await expect(page.getByText("You don't run a league here yet.")).toBeVisible();
  await expect(page.getByRole("heading", { name: /^(Members|Join link|Asking to join|Managers)/ })).toHaveCount(0);
  await expectNoSeriousA11yViolations(page);

  await createLeague(page, own, undefined, dates);
  const ownPath = pathOf(page);
  // Theirs, by name alone: no emails, and no say over who else runs it.
  const managers = page.getByRole("list", { name: "Managers of this league" });
  await expect(managers).toContainText(name);
  await expect(managers).not.toContainText("@");
  await expect(page.getByLabel("Give this league to")).toHaveCount(0);
  await expectNoSeriousA11yViolations(page);

  await page.goto("/manage");
  await expect(page.getByRole("link", { name: own })).toBeVisible();
  await expect(page.getByRole("link", { name: clubs })).toHaveCount(0);
  // Another's League in the Club: they can see there is one (403), and it isn't theirs to open.
  await page.goto(clubsPath);
  await expect(page.getByRole("heading", { name: "Page not found" })).toBeVisible();

  // A manager of the Club can take it back, and the role stays: they may create another.
  await manager.goto(ownPath);
  const seen = manager.getByRole("list", { name: "Managers of this league" });
  await expect(seen).toContainText(name);
  await expect(seen).toContainText(email);
  await manager.getByRole("button", { name: `Remove ${name} as a manager of ${own}` }).click();
  await expect(manager.getByText("Run by the club's managers.")).toBeVisible();
  await page.goto(ownPath);
  await expect(page.getByRole("heading", { name: "Page not found" })).toBeVisible();

  await manager.goto("/manage");
  await manager.getByRole("button", { name: `Remove ${email} as league manager` }).click();
  await expect(manager.getByRole("button", { name: `Remove ${email} as league manager` })).toHaveCount(0);
  await page.goto("/manage");
  await expect(page.getByRole("heading", { name: "Page not found" })).toBeVisible();
});

test("a League given to a member names them to its Players, and is theirs to run", async ({ page, browser }, testInfo) => {
  test.slow();
  const league = uniqueName(testInfo, "Given League");
  const runner = uniqueName(testInfo, "Gwen Given");
  const runnerEmail = personaEmail(testInfo, "gwen");
  const player = uniqueName(testInfo, "Pat Player");
  const playerEmail = personaEmail(testInfo, "patplayer");

  const manager = await (await browser.newContext()).newPage();
  await logIn(manager, testInfo, personaEmail(testInfo, "manager"));
  await addClubPlayer(manager, runner, runnerEmail);
  await addClubPlayer(manager, player, playerEmail);
  await createLeague(manager, league, undefined, dates);
  await expect(manager.getByText("Run by the club's managers.")).toBeVisible();
  await manager.getByLabel("Player", { exact: true }).selectOption({ label: `${player} (${playerEmail})` });
  await manager.getByRole("button", { name: "Add to league" }).click();
  await expect(manager.getByRole("list", { name: "Players in the league" })).toContainText(player);

  await manager.getByLabel("Give this league to").selectOption({ label: `${runner} (${runnerEmail})` });
  await manager.getByRole("button", { name: "Make league manager" }).click();
  await expect(manager.getByRole("list", { name: "Managers of this league" })).toContainText(runner);
  await expectNoSeriousA11yViolations(manager);
  const ladder = `/leagues/${pathOf(manager).split("/").at(-1)}`;

  // Its Players see a name, and never an email.
  await logIn(page, testInfo, playerEmail);
  await page.goto(ladder);
  await expect(page.getByText(`Run by ${runner}`)).toBeVisible();
  await expect(page.getByRole("main")).not.toContainText("@");
  await expect(page.getByRole("link", { name: "Manage this league" })).toHaveCount(0);

  // It is theirs: the ladder has a way to the Manager's page, and nothing else of the Club's.
  const runnerPage = await (await browser.newContext()).newPage();
  await logIn(runnerPage, testInfo, runnerEmail);
  await runnerPage.goto(ladder);
  await runnerPage.getByRole("link", { name: "Manage this league" }).click();
  await expect(runnerPage.getByRole("heading", { name: league, level: 1 })).toBeVisible();
  await expect(runnerPage.getByLabel("Give this league to")).toHaveCount(0);
  await runnerPage.goto("/manage");
  await expect(runnerPage.getByRole("link", { name: "New league" })).toHaveCount(0);
  await expect(runnerPage.getByRole("link", { name: league })).toBeVisible();
});

test("giving a League to someone who has just left the Club says so, and gives nothing", async ({ page, browser }, testInfo) => {
  test.slow();
  const league = uniqueName(testInfo, "Contested League");
  const name = uniqueName(testInfo, "Lena Leaving");
  const email = personaEmail(testInfo, "lena");
  await logIn(page, testInfo, personaEmail(testInfo, "manager"));
  await addClubPlayer(page, name, email);
  await createLeague(page, league, undefined, dates);
  const leaguePath = pathOf(page);
  await page.getByLabel("Give this league to").selectOption({ label: `${name} (${email})` });

  // Meanwhile another of the Club's managers takes them out of it.
  const other = await (await browser.newContext()).newPage();
  await logIn(other, testInfo, personaEmail(testInfo, "manager"));
  const takeOut = async (who: string) => {
    await other.goto("/manage");
    await other.getByRole("button", { name: `Remove ${who}`, exact: true }).click();
    await other.getByRole("dialog", { name: `Remove ${who}?` }).getByRole("button", { name: "Remove from club" }).click();
    await expect(other.getByRole("button", { name: `Remove ${who}`, exact: true })).toHaveCount(0);
  };
  await takeOut(name);

  await page.getByRole("button", { name: "Make league manager" }).click();
  await expect(page.getByText("That wasn't given")).toBeVisible();
  await expect(page.getByText("Run by the club's managers.")).toBeVisible();

  // Given to someone who then leaves: they have no Player to be named by, and the button that takes it back says whose.
  const ladder = `/leagues/${leaguePath.split("/").at(-1)}`;
  const giveTo = async (who: string, address: string) => {
    await addClubPlayer(page, who, address);
    await page.goto(leaguePath);
    await page.getByLabel("Give this league to").selectOption({ label: `${who} (${address})` });
    await page.getByRole("button", { name: "Make league manager" }).click();
    await expect(page.getByRole("button", { name: `Remove ${who} as a manager of ${league}` })).toBeVisible();
    await takeOut(who);
  };
  const moeEmail = personaEmail(testInfo, "moe");
  await giveTo(uniqueName(testInfo, "Moe Leaving"), moeEmail);
  await page.goto(leaguePath);
  await expect(page.getByRole("list", { name: "Managers of this league" })).toContainText("A manager");
  await expect(page.getByRole("button", { name: `Remove ${moeEmail} as a manager of ${league}` })).toBeVisible();
  await page.goto(ladder);
  await expect(page.getByText("Run by a manager", { exact: true })).toBeVisible();

  // Two of them are counted, not repeated.
  await giveTo(uniqueName(testInfo, "Ned Leaving"), personaEmail(testInfo, "ned"));
  await page.goto(ladder);
  await expect(page.getByText("Run by 2 managers", { exact: true })).toBeVisible();
});

test("a League manager who isn't in the Club is told how to get in, not to pre-approve themselves", async ({ page }, testInfo) => {
  // The mock's persona has the role already; the real API gives it to someone with a Player in the Club, who then leaves.
  const email = personaEmail(testInfo, "league-manager");
  await logIn(page, testInfo, email);
  if (testInfo.project.name === "mock") {
    await addFirstPlayer(page, uniqueName(testInfo, "Lee Outside"));
  } else {
    const club = process.env.E2E_CLUB_ID;
    const added = (await (
      await asAdmin(`/clubs/${club}/players`, "POST", { name: uniqueName(testInfo, "Lee Outside"), email })
    ).json()) as { playerId: string };
    await asAdmin(`/clubs/${club}/league-managers/${await userIdOf(email)}`, "PUT");
    await asAdmin(`/clubs/${club}/members/${added.playerId}`, "DELETE");
    await page.reload();
  }

  // They manage something, so Manage is there; but they can't let anyone into a Club, themselves included.
  await expect(page.getByRole("navigation", { name: "Main" }).getByRole("link", { name: "Manage" })).toBeVisible();
  await expect(page.getByText(/Ask your club's manager for the club's join link/)).toBeVisible();
  await expect(page.getByText(/pre-approve/)).toHaveCount(0);
  await expect(page.getByRole("link", { name: "Go to Manage" })).toHaveCount(0);

  // Manage has the Club whose role they hold, and no League of theirs in it yet.
  await page.goto("/manage");
  await expect(page.getByText("You don't run a league here yet.")).toBeVisible();
});
