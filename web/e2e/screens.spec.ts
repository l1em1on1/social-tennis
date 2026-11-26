// Chromatic's test captures a DOM snapshot at the end of every test (mock project only;
// see playwright.config.ts), through e2e/support/chromatic.ts. Otherwise identical to
// @playwright/test.
import { expect, test } from "./support/chromatic";
import type { Page, TestInfo } from "@playwright/test";
import {
  FIXTURE_ADMIN,
  FIXTURE_GAME_IDS,
  FIXTURE_HELD_UP_LEAGUE_ID,
  FIXTURE_JOIN_CODE,
  FIXTURE_LEAGUE_MANAGER,
  FIXTURE_MANAGER,
  FIXTURE_MEMBER,
  FIXTURE_NEWCOMER,
  FIXTURE_OUTSIDER,
  FIXTURE_RUNNING_GAME_IDS,
  FIXTURE_RUNNING_LEAGUE_ID,
  FIXTURE_STARTED_LEAGUE_ID,
  FIXTURE_WAITING,
  RIVERSIDE,
} from "../mock-api/fixture";
import { logIn } from "./support/auth";
import { expectNoSeriousA11yViolations } from "./support/axe";

/**
 * The app's screens, from the mock API's fixed world (#234): one snapshot per
 * screen and state, each as the person who sees it. Nothing here changes
 * anything, so each runs alone and looks the same every time. What the
 * screens do is tested against the real API, by the other specs.
 * The Manager's and Admin's pages are in manager-pages.spec.ts.
 */
test.beforeEach(({}, testInfo) => {
  test.skip(testInfo.project.name !== "mock", "the fixed data is the mock's");
});

const h1 = (page: Page, name: string | RegExp) => page.getByRole("heading", { name, level: 1 });

async function as(page: Page, testInfo: TestInfo, email: string, path: string) {
  await logIn(page, testInfo, email);
  if (path !== "/") await page.goto(path);
}

test.describe("signed out", () => {
  test("the login page", async ({ page }) => {
    await page.goto("/login");
    await expect(page.getByRole("button", { name: "Email me a sign-in link" })).toBeVisible();
    await expectNoSeriousA11yViolations(page);
  });

  test("the login page once a link is sent", async ({ page }) => {
    await page.goto("/login");
    await page.getByLabel("Email address").fill("alex.morgan@example.com");
    await page.getByRole("button", { name: "Email me a sign-in link" }).click();
    await expect(page.getByRole("heading", { name: "Check your email" })).toBeVisible();
  });

  test("the page an emailed sign-in link opens", async ({ page }) => {
    await page.goto(`/auth/verify?token=${encodeURIComponent(FIXTURE_MEMBER)}`);
    await expect(page.getByRole("button", { name: "Sign in", exact: true })).toBeVisible();
  });

  test("the privacy notice", async ({ page }) => {
    await page.goto("/privacy");
    await expect(h1(page, "Privacy notice")).toBeVisible();
  });

  test("a Club's join link, signed out", async ({ page }) => {
    await page.goto(`/join/${FIXTURE_JOIN_CODE}`);
    await expect(page.getByText("Sign in first, then you can ask to join the club.")).toBeVisible();
  });
});

test("a join link that doesn't work", async ({ page }, testInfo) => {
  await as(page, testInfo, FIXTURE_OUTSIDER, "/join/NoSuchCodeAnyWhere123");
  await expect(page.getByRole("heading", { name: "This link doesn't work any more" })).toBeVisible();
});

test.describe("someone new", () => {
  test("Home on a first sign-in, asking for a name", async ({ page }, testInfo) => {
    await as(page, testInfo, FIXTURE_NEWCOMER, "/");
    await expect(page.getByRole("heading", { name: "Welcome! What's your name?" })).toBeVisible();
    await expectNoSeriousA11yViolations(page);
  });

  test("Profiles before there is a Player", async ({ page }, testInfo) => {
    await as(page, testInfo, FIXTURE_NEWCOMER, "/profiles");
    await expect(h1(page, "Profiles")).toBeVisible();
  });

  test("Home for a Player in no Club", async ({ page }, testInfo) => {
    await as(page, testInfo, FIXTURE_OUTSIDER, "/");
    await expect(page.getByRole("heading", { name: "Join your club" })).toBeVisible();
  });

  test("a Club's join link, for a Player in no Club", async ({ page }, testInfo) => {
    await as(page, testInfo, FIXTURE_OUTSIDER, `/join/${FIXTURE_JOIN_CODE}`);
    await expect(page.getByRole("button", { name: `Ask to join ${RIVERSIDE.name}` })).toBeVisible();
    await expectNoSeriousA11yViolations(page);
  });

  test("Home while a request to join waits", async ({ page }, testInfo) => {
    await as(page, testInfo, FIXTURE_WAITING, "/");
    await expect(page.getByRole("heading", { name: "Waiting to join" })).toBeVisible();
  });
});

test.describe("a member, with a child on the same login", () => {
  test("Home with games to score", async ({ page }, testInfo) => {
    await as(page, testInfo, FIXTURE_MEMBER, "/");
    await expect(page.getByRole("list", { name: "Your games in Summer Ladder" })).toBeVisible();
    await expectNoSeriousA11yViolations(page);
  });

  test("the Leagues page: Leagues joined, and one to join with a partner", async ({ page }, testInfo) => {
    await as(page, testInfo, FIXTURE_MEMBER, "/leagues");
    await expect(page.getByRole("region", { name: "You can join" })).toBeVisible();
    await expectNoSeriousA11yViolations(page);
  });

  test("a ladder, as a Player in it", async ({ page }, testInfo) => {
    await as(page, testInfo, FIXTURE_MEMBER, `/leagues/${FIXTURE_RUNNING_LEAGUE_ID}`);
    await expect(page.getByRole("status")).toContainText("You're in Division 2");
    await expectNoSeriousA11yViolations(page);
  });

  test("a ladder whose League has ended, as a Player in it", async ({ page }, testInfo) => {
    await as(page, testInfo, FIXTURE_MEMBER, `/leagues/${FIXTURE_STARTED_LEAGUE_ID}`);
    await expect(page.getByRole("note")).toContainText("The League has ended");
  });

  test("a Game with no score, as a Player in it", async ({ page }, testInfo) => {
    await as(page, testInfo, FIXTURE_MEMBER, `/games/${FIXTURE_RUNNING_GAME_IDS.chrisAlex}`);
    await expect(page.getByRole("link", { name: "Enter score" })).toBeVisible();
  });

  test("a Game two others play, as a Player in the League", async ({ page }, testInfo) => {
    await as(page, testInfo, FIXTURE_MEMBER, `/games/${FIXTURE_RUNNING_GAME_IDS.joElla}`);
    await expect(h1(page, "Jo Bennett v Ella Grant")).toBeVisible();
    await expect(page.getByRole("link", { name: "Enter score" })).toHaveCount(0);
  });

  test("entering a score", async ({ page }, testInfo) => {
    await as(page, testInfo, FIXTURE_MEMBER, `/games/${FIXTURE_RUNNING_GAME_IDS.chrisAlex}/score`);
    const more = page.getByRole("group", { name: "Set, Alex Morgan" }).getByRole("button", { name: "One more" });
    for (let games = 0; games < 6; games++) await more.click();
    await expect(page.getByLabel("Score so far")).toHaveText("0–6");
    await expectNoSeriousA11yViolations(page);
  });

  test("a Game whose Stage has closed, as a Player in it", async ({ page }, testInfo) => {
    await as(page, testInfo, FIXTURE_MEMBER, `/games/${FIXTURE_GAME_IDS.played}`);
    await expect(page.getByRole("note")).toContainText("can't be changed any more");
  });

  test("Profiles with two Players on one login", async ({ page }, testInfo) => {
    await as(page, testInfo, FIXTURE_MEMBER, "/profiles");
    await expect(page.getByRole("button", { name: "Edit Sam Morgan" })).toBeVisible();
    await expectNoSeriousA11yViolations(page);
  });

  test("the Play as sheet", async ({ page }, testInfo) => {
    await as(page, testInfo, FIXTURE_MEMBER, "/profiles");
    await page.getByRole("button", { name: /switch player/ }).click();
    await expect(page.getByRole("dialog", { name: "Play as" })).toBeVisible();
  });

  test("the Leagues page for the child, with a request waiting", async ({ page }, testInfo) => {
    await as(page, testInfo, FIXTURE_MEMBER, "/profiles");
    await page.getByRole("button", { name: /switch player/ }).click();
    await page.getByRole("dialog", { name: "Play as" }).getByRole("button", { name: "Sam Morgan" }).click();
    await expect(page.getByTestId("acting-player")).toHaveText("Sam Morgan");
    await page.goto("/leagues");
    await expect(page.getByText("Request sent")).toBeVisible();
  });

  test("Manage, which a Player can't open", async ({ page }, testInfo) => {
    await as(page, testInfo, FIXTURE_MEMBER, "/manage");
    await expect(h1(page, /not found|isn't here/i)).toBeVisible();
  });
});

test.describe("a League manager who plays too", () => {
  test("Home with a Stage held up", async ({ page }, testInfo) => {
    await as(page, testInfo, FIXTURE_LEAGUE_MANAGER, "/");
    await expect(page.getByRole("region", { name: "Spring Ladder: Stage held up" })).toBeVisible();
    await expectNoSeriousA11yViolations(page);
  });

  test("Manage, as a League manager", async ({ page }, testInfo) => {
    await as(page, testInfo, FIXTURE_LEAGUE_MANAGER, "/manage");
    await expect(page.getByRole("link", { name: "Summer Ladder" })).toBeVisible();
  });

  test("a new League", async ({ page }, testInfo) => {
    await as(page, testInfo, FIXTURE_LEAGUE_MANAGER, `/manage/leagues/new?club=${RIVERSIDE.id}`);
    await expect(page.getByRole("button", { name: "Create League" })).toBeVisible();
    await expectNoSeriousA11yViolations(page);
  });

  test("a running League's page, as the League manager who runs it", async ({ page }, testInfo) => {
    await as(page, testInfo, FIXTURE_LEAGUE_MANAGER, `/manage/leagues/${FIXTURE_RUNNING_LEAGUE_ID}`);
    await expect(page.getByRole("list", { name: "Games with no score" })).toBeVisible();
    await expectNoSeriousA11yViolations(page);
  });

  test("a Game with no score, as the League manager", async ({ page }, testInfo) => {
    await as(page, testInfo, FIXTURE_LEAGUE_MANAGER, `/games/${FIXTURE_RUNNING_GAME_IDS.ellaNia}`);
    await expect(page.getByRole("button", { name: "Mark not played (manager)" })).toBeVisible();
  });

  test("a ladder held up, as a Player in it", async ({ page }, testInfo) => {
    await as(page, testInfo, FIXTURE_LEAGUE_MANAGER, `/leagues/${FIXTURE_HELD_UP_LEAGUE_ID}`);
    await expect(page.getByRole("region", { name: "Stage held up" })).toBeVisible();
    await expectNoSeriousA11yViolations(page);
  });
});

test.describe("a Club's manager", () => {
  test("a held-up League's page", async ({ page }, testInfo) => {
    await as(page, testInfo, FIXTURE_MANAGER, `/manage/leagues/${FIXTURE_HELD_UP_LEAGUE_ID}`);
    await expect(page.getByRole("list", { name: "Ties that decide who moves" })).toBeVisible();
  });

  test("closing a Stage: a tie to order, and a Team waiting for a place", async ({ page }, testInfo) => {
    await as(page, testInfo, FIXTURE_MANAGER, `/manage/leagues/${FIXTURE_HELD_UP_LEAGUE_ID}/close`);
    await expect(page.getByRole("button", { name: "Move Ella Grant up" })).toBeVisible();
    await expectNoSeriousA11yViolations(page);
  });

  test("closing a Stage with games still to settle", async ({ page }, testInfo) => {
    await as(page, testInfo, FIXTURE_MANAGER, `/manage/leagues/${FIXTURE_RUNNING_LEAGUE_ID}/close`);
    await expect(page.getByRole("region", { name: "Settle these first" })).toBeVisible();
  });
});

test.describe("an Admin", () => {
  test("Admin, searched for one name", async ({ page }, testInfo) => {
    await as(page, testInfo, FIXTURE_ADMIN, "/admin?q=morgan");
    await expect(page.getByText(/matching “morgan”/)).toBeVisible();
  });

  test("Admin, one Club's Users", async ({ page }, testInfo) => {
    await as(page, testInfo, FIXTURE_ADMIN, `/admin?club=${RIVERSIDE.id}`);
    await expect(page.getByLabel("Club", { exact: true })).toHaveValue(RIVERSIDE.id);
  });
});

/** The wide layouts (#225): Chromatic snapshots each test at its own viewport. */
test.describe("on a desktop screen", () => {
  test.use({ viewport: { width: 1280, height: 800 }, isMobile: false, hasTouch: false });

  test("Home with games to score, on a desktop", async ({ page }, testInfo) => {
    await as(page, testInfo, FIXTURE_MEMBER, "/");
    await expect(page.getByRole("list", { name: "Your games in Summer Ladder" })).toBeVisible();
  });

  test("the Leagues page, on a desktop", async ({ page }, testInfo) => {
    await as(page, testInfo, FIXTURE_MEMBER, "/leagues");
    await expect(page.getByRole("region", { name: "You can join" })).toBeVisible();
  });

  test("a running League's page, on a desktop", async ({ page }, testInfo) => {
    await as(page, testInfo, FIXTURE_LEAGUE_MANAGER, `/manage/leagues/${FIXTURE_RUNNING_LEAGUE_ID}`);
    await expect(page.getByRole("list", { name: "Games with no score" })).toBeVisible();
  });

  test("closing a Stage, on a desktop", async ({ page }, testInfo) => {
    await as(page, testInfo, FIXTURE_MANAGER, `/manage/leagues/${FIXTURE_HELD_UP_LEAGUE_ID}/close`);
    await expect(page.getByRole("button", { name: "Move Ella Grant up" })).toBeVisible();
  });
});
