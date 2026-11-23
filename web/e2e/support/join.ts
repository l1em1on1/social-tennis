import { expect, type Page } from "@playwright/test";

/**
 * The first Club on Manage and its join link, as its Manager copies it (#222),
 * turned on if it's off. Turning on keeps a link that's on, so the tests
 * sharing this Club share its link; none of them resets it or turns it off.
 * Returns the link's path, since each project serves the app at its own address.
 */
export async function sharedJoinLink(manager: Page): Promise<{ path: string; clubName: string }> {
  await manager.goto("/manage");
  // The first Club listed: the Admin specs create others, which sort after it.
  const clubName = (await manager.getByRole("heading", { level: 2 }).first().textContent()) ?? "";
  const section = manager.getByRole("region", { name: "Join link" }).first();
  const turnOn = section.getByRole("button", { name: "Turn on the join link" });
  if (await turnOn.isVisible()) {
    await turnOn.click();
  }
  const field = section.getByLabel(`Link to ask to join ${clubName}`);
  await expect(field).toBeVisible();
  return { path: new URL(await field.inputValue()).pathname, clubName };
}
