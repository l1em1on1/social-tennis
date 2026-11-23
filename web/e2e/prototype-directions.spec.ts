// PROTOTYPE (#226) — throwaway: photographs real screens in each design
// direction (src/app/prototype-directions.css), light and dark, at 360px, for
// the owner to pick from. Mock project only. Not for main.
import { expect, test } from "./support/chromatic";
import type { Page } from "@playwright/test";
import { addFirstPlayer, logIn, personaEmail } from "./support/auth";
import { FIXTURE_GAME_IDS, FIXTURE_MANAGER, FIXTURE_STARTED_LEAGUE_ID } from "../src/mocks/fixture";

test.use({ disableAutoSnapshot: true });

const directions = ["current", "lime", "clay", "grass"] as const;
const modes = ["light", "dark"] as const;

async function shoot(page: Page, screen: string) {
  for (const mode of modes) {
    await page.emulateMedia({ colorScheme: mode });
    for (const direction of directions) {
      await page.evaluate((d) => {
        if (d === "current") delete document.documentElement.dataset.direction;
        else document.documentElement.dataset.direction = d;
      }, direction);
      await page.waitForTimeout(150);
      await page.screenshot({ path: `test-results/directions/${screen}-${direction}-${mode}.png`, fullPage: true });
    }
  }
}

test("design directions: Home, ladder, Manage and a Match, in each direction", async ({ page, browser }, testInfo) => {
  test.skip(testInfo.project.name !== "mock", "the fixed data is the mock's");
  test.setTimeout(180_000);

  // Home: a Player with a League under way, owing a score.
  const manager = await (await browser.newContext()).newPage();
  await logIn(manager, testInfo, personaEmail(testInfo, "manager"));
  for (const [name, email] of [["Una Directions", "una-directions@example.com"], ["Vic Directions", "vic-directions@example.com"]]) {
    await manager.goto("/manage");
    await manager.getByLabel("Name (optional)").first().fill(name);
    await manager.getByLabel("Their email address").first().fill(email);
    await manager.getByRole("button", { name: "Add", exact: true }).first().click();
    await expect(manager.getByRole("status")).toContainText(`${name} is added`);
  }
  await manager.goto("/manage");
  await manager.getByRole("link", { name: "New league" }).first().click();
  await manager.getByLabel("Name", { exact: true }).fill("Winter Ladder");
  await manager.getByLabel("Starts").fill("2026-11-01");
  await manager.getByLabel("Ends").fill("2030-12-31");
  await manager.getByLabel("Stage length", { exact: true }).fill("12");
  await manager.getByRole("button", { name: "Create League" }).click();
  await expect(manager.getByRole("heading", { name: "Winter Ladder", level: 1 })).toBeVisible();
  for (const [name, email] of [["Una Directions", "una-directions@example.com"], ["Vic Directions", "vic-directions@example.com"]]) {
    await manager.getByLabel("Player", { exact: true }).selectOption({ label: `${name} (${email})` });
    await manager.getByRole("button", { name: "Add to league" }).click();
  }
  await manager.getByRole("link", { name: "Start the league" }).click();
  await manager.getByRole("button", { name: "Confirm and start" }).click();
  await expect(manager.getByRole("status").filter({ hasText: "has started" })).toBeVisible();

  await logIn(page, testInfo, "una-directions@example.com");
  await expect(page.getByRole("list", { name: "Your games in Winter Ladder" })).toBeVisible();
  await shoot(page, "home");

  // A Player new to the app: the empty states.
  await page.context().clearCookies();
  await logIn(page, testInfo, personaEmail(testInfo, "newcomer"));
  await addFirstPlayer(page, "Nell Newcomer");
  await shoot(page, "newcomer");

  // The fixed world: a ladder with standings, Manage, and a Match.
  await page.context().clearCookies();
  await logIn(page, testInfo, FIXTURE_MANAGER);
  await page.goto(`/leagues/${FIXTURE_STARTED_LEAGUE_ID}`);
  await expect(page.getByRole("heading", { name: "Autumn Singles", level: 1 })).toBeVisible();
  await shoot(page, "ladder");
  await page.goto("/manage");
  await expect(page.getByRole("heading", { name: "Manage", level: 1 })).toBeVisible();
  await shoot(page, "manage");
  await page.goto(`/games/${FIXTURE_GAME_IDS.played}`);
  await expect(page.getByRole("region", { name: "Score" })).toBeVisible();
  await shoot(page, "match");
});
