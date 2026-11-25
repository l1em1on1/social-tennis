// Chromatic's test captures a DOM snapshot at the end of every test (mock project only;
// see playwright.config.ts), through e2e/support/chromatic.ts. Otherwise identical to
// @playwright/test.
import { expect, test } from "./support/chromatic";
import { addFirstPlayer, asAdmin, logIn, personaEmail, uniqueName, userIdOf } from "./support/auth";

// The pages a League manager sees (#220).
test.use({ disableAutoSnapshot: true });

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

  await page.goto("/manage");
  await expect(page.getByText("No club to manage")).toBeVisible();
});
