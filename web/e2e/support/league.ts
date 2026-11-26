import { expect, type Page } from "@playwright/test";

/** On Manage: adds a Player to the first Club for an email nobody has signed in with yet. */
export async function addClubPlayer(manager: Page, name: string, email: string) {
  await manager.goto("/manage");
  await manager.getByLabel("Name (optional)").first().fill(name);
  await manager.getByLabel("Their email address").first().fill(email);
  await manager.getByRole("button", { name: "Add", exact: true }).first().click();
  await expect(manager.getByRole("status")).toContainText(`${name} is added`);
}

/**
 * From Manage: creates a League in the first Club with the setup form's
 * defaults (or another Type, other dates, Stages of more months, or another
 * way to rank its Standings), and lands on its page.
 *
 * A League a test starts should keep its Stage 1 deadline far off: once it
 * passes, the ladder shows the "held up" banner (#10), and the snapshot of
 * the page would change with the real date.
 */
export async function createLeague(
  manager: Page,
  name: string,
  type?: string,
  {
    starts = "2026-12-01",
    ends = "2027-03-31",
    stageMonths,
    standings,
  }: { starts?: string; ends?: string; stageMonths?: number; standings?: "Bonus points" } = {},
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
  if (standings) await manager.getByRole("group", { name: "Standings" }).getByText(standings).click();
  await manager.getByRole("button", { name: "Create League" }).click();
  await expect(manager.getByRole("heading", { name, level: 1 })).toBeVisible();
}
