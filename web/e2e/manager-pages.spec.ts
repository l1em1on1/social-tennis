// Chromatic's test captures a DOM snapshot at the end of every test (mock project only;
// see playwright.config.ts), through e2e/support/chromatic.ts, which keeps the bottom
// nav off a long page's content (#182). Otherwise identical to @playwright/test.
import { expect, test } from "./support/chromatic";
import {
  FIXTURE_ADMIN,
  FIXTURE_DOUBLES_LEAGUE_ID,
  FIXTURE_GAME_IDS,
  FIXTURE_LEAGUE_ID,
  FIXTURE_MANAGER,
  FIXTURE_STARTED_LEAGUE_ID,
} from "../src/mocks/fixture";
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

test("a doubles League's Manager page, from fixed data", async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== "mock", "the fixed data is the mock's");
  await logIn(page, testInfo, FIXTURE_MANAGER);
  await page.goto(`/manage/leagues/${FIXTURE_DOUBLES_LEAGUE_ID}`);

  await expect(page.getByRole("heading", { name: "Spring Doubles", level: 1 })).toBeVisible();
  await expect(page.getByRole("list", { name: "Teams in the league" }).getByRole("listitem")).toHaveCount(1);
  await expect(page.getByRole("list", { name: "Players without a partner" }).getByRole("listitem")).toHaveCount(2);
  // A pair asking together (#6), both women in mixed doubles: a warning, not a refusal.
  await expect(page.getByRole("button", { name: "Approve Nia Brooks with Ella Grant" })).toBeVisible();
  await expect(page.getByText(/Both are the same gender/)).toBeVisible();
  await expectNoSeriousA11yViolations(page);
});

test("starting a League, from fixed data", async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== "mock", "the fixed data is the mock's");
  await logIn(page, testInfo, FIXTURE_MANAGER);
  await page.goto(`/manage/leagues/${FIXTURE_LEAGUE_ID}/start`);

  await expect(page.getByRole("heading", { name: "Start Winter Singles", level: 1 })).toBeVisible();
  await expect(page.getByRole("list", { name: "Division 1" }).getByRole("listitem")).toHaveCount(2);
  await expect(page.getByText("1 join request is still waiting.", { exact: false })).toBeVisible();
  // Every Rating is 1, so the proposal is the order they joined, and the screen says so.
  await expect(page.getByText("Proposed in the order players joined", { exact: false })).toBeVisible();
  await expectNoSeriousA11yViolations(page);
});

test("a started League's Manager page, from fixed data", async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== "mock", "the fixed data is the mock's");
  await logIn(page, testInfo, FIXTURE_MANAGER);
  await page.goto(`/manage/leagues/${FIXTURE_STARTED_LEAGUE_ID}`);

  await expect(page.getByRole("heading", { name: "Autumn Singles", level: 1 })).toBeVisible();
  await expect(page.getByText("Stage 1 runs 1 Oct 2026 – 31 Oct 2026.")).toBeVisible();
  await expect(page.getByRole("link", { name: "Start the league" })).toHaveCount(0);
  const players = page.getByRole("list", { name: "Players in the league" });
  await expect(players.getByRole("listitem").filter({ hasText: "Chris Lee" })).toContainText("Division 1");
  // On the ladder, leaving is a Withdrawal (#21): only Sam, left out, can be removed.
  await expect(players.getByRole("button", { name: /^Remove/ })).toHaveCount(1);
  await expect(players.getByRole("listitem").filter({ hasText: "Sam Morgan" })).toContainText("Not on the ladder");
  await expectNoSeriousA11yViolations(page);
});

test("a started League's ladder, from fixed data", async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== "mock", "the fixed data is the mock's");
  await logIn(page, testInfo, FIXTURE_MANAGER);
  await page.goto(`/leagues/${FIXTURE_STARTED_LEAGUE_ID}`);

  await expect(page.getByRole("heading", { name: "Autumn Singles", level: 1 })).toBeVisible();
  const division = page.getByRole("region", { name: "Division 1" });
  // Chris and Jo both won once; Chris by more games (#10). One Division, so nobody moves.
  const standings = division.getByRole("table", { name: "Division 1 standings" }).getByRole("row").filter({ has: page.getByRole("rowheader") });
  await expect(standings).toHaveText([/^1Chris Lee110\+2stays$/, /^2Jo Bennett211-1stays$/, /^3Alex Morgan101-1stays$/]);
  await expect(division.getByRole("list", { name: "Division 1 Games" }).getByRole("listitem")).toHaveText([
    /Jo Bennett v Alex Morgan/,
    /Jo Bennett v Chris Lee/,
    /Alex Morgan v Chris Lee/,
  ]);
  await expectNoSeriousA11yViolations(page);
});

test("a Game's Match screen, from fixed data", async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== "mock", "the fixed data is the mock's");
  await logIn(page, testInfo, FIXTURE_MANAGER);

  // On the ladder: a tiebreak set, a retirement, and a Game not played (#10).
  await page.goto(`/leagues/${FIXTURE_STARTED_LEAGUE_ID}`);
  await expect(page.getByRole("list", { name: "Division 1 Games" }).getByRole("listitem")).toHaveText([
    /Jo Bennett v Alex Morgan.*6–5\(4\).*Played/,
    /Jo Bennett v Chris Lee.*2–4.*Retirement/,
    /Alex Morgan v Chris Lee.*Void/,
  ]);

  await page.goto(`/games/${FIXTURE_GAME_IDS.retirement}`);
  await expect(page.getByRole("heading", { name: "Jo Bennett v Chris Lee", level: 1 })).toBeVisible();
  const score = page.getByRole("region", { name: "Score" });
  await expect(score).toContainText("Retirement");
  await expect(score).toContainText("Chris Lee won when the game stopped early · played 12 Oct 2026 · entered by Jo Bennett");

  // Marked not played before October closed (#10): it counts for nothing, and can't be undone now.
  await page.goto(`/games/${FIXTURE_GAME_IDS.notPlayed}`);
  await expect(page.getByRole("region", { name: "Not played" })).toContainText("it counts for nothing, for either side");
  await expect(page.getByRole("button", { name: /Undo/ })).toHaveCount(0);

  // Entered, then corrected; the Manager can override it after the close (#38).
  await page.goto(`/games/${FIXTURE_GAME_IDS.played}`);
  const rows = page.getByRole("region", { name: "Score" }).getByRole("row").filter({ has: page.getByRole("rowheader") });
  await expect(rows).toHaveText([/Jo Bennett.*6/, /Alex Morgan.*5\(4\)/]);
  await expect(page.getByRole("list", { name: "Changes to the score" }).getByRole("listitem")).toHaveText([
    /Jo Bennett corrected it · 9 Oct, 08:15.*5–6\(4\) · Alex Morgan won.*6–5\(4\) · Jo Bennett won/,
    /Alex Morgan entered it · 8 Oct, 20:40/,
  ]);
  await expect(page.getByRole("region", { name: "Score" })).toContainText("Stage 1 is closed. An override is kept in the score's history.");
  await expectNoSeriousA11yViolations(page);
  await page.getByRole("link", { name: "Override score (manager)" }).click();
  await expect(page.getByRole("heading", { name: "Override the score", level: 1 })).toBeVisible();
  await expect(page.getByLabel("Score so far")).toHaveText("6–5(4)");
  // Nothing to save until something changes.
  await expect(page.getByRole("status")).toHaveText("Change something to save an override.");
  await expect(page.getByRole("button", { name: "Save override" })).toBeDisabled();
});

test("Admin, from fixed data", async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== "mock", "the fixed data is the mock's");
  await logIn(page, testInfo, FIXTURE_ADMIN);
  await page.goto("/admin");

  await expect(page.getByRole("heading", { name: "Admin", level: 1 })).toBeVisible();
  await expect(page.getByText("Users (9)")).toBeVisible();
  await expectNoSeriousA11yViolations(page);
});

/**
 * The same fixed world on a desktop screen (#225): Chromatic snapshots each
 * test at its own viewport, so these review the wide layouts, where the nav is
 * a top bar and the pages lay out in columns.
 */
test.describe("on a desktop screen", () => {
  test.use({ viewport: { width: 1280, height: 800 }, isMobile: false, hasTouch: false });

  test("Manage, from fixed data, on a desktop", async ({ page }, testInfo) => {
    test.skip(testInfo.project.name !== "mock", "the fixed data is the mock's");
    await logIn(page, testInfo, FIXTURE_MANAGER);
    await page.goto("/manage");

    await expect(page.getByRole("heading", { name: "Manage", level: 1 })).toBeVisible();
    await expect(page.getByRole("list", { name: "Players of alex.morgan@example.com" })).toBeVisible();
    await expectNoSeriousA11yViolations(page);
  });

  test("a started League's Manager page, from fixed data, on a desktop", async ({ page }, testInfo) => {
    test.skip(testInfo.project.name !== "mock", "the fixed data is the mock's");
    await logIn(page, testInfo, FIXTURE_MANAGER);
    await page.goto(`/manage/leagues/${FIXTURE_STARTED_LEAGUE_ID}`);

    await expect(page.getByRole("heading", { name: "Autumn Singles", level: 1 })).toBeVisible();
    await expectNoSeriousA11yViolations(page);
  });

  test("a started League's ladder, from fixed data, on a desktop", async ({ page }, testInfo) => {
    test.skip(testInfo.project.name !== "mock", "the fixed data is the mock's");
    await logIn(page, testInfo, FIXTURE_MANAGER);
    await page.goto(`/leagues/${FIXTURE_STARTED_LEAGUE_ID}`);

    await expect(page.getByRole("table", { name: "Division 1 standings" })).toBeVisible();
    await expectNoSeriousA11yViolations(page);
  });

  test("Admin, from fixed data, on a desktop", async ({ page }, testInfo) => {
    test.skip(testInfo.project.name !== "mock", "the fixed data is the mock's");
    await logIn(page, testInfo, FIXTURE_ADMIN);
    await page.goto("/admin");

    await expect(page.getByText("Users (9)")).toBeVisible();
    await expectNoSeriousA11yViolations(page);
  });
});
