// Chromatic's test captures a DOM snapshot at the end of every test (mock project only;
// see playwright.config.ts), through e2e/support/chromatic.ts. Otherwise identical to
// @playwright/test.
import { expect, test } from "./support/chromatic";
import { asAdmin, logIn, personaEmail, uniqueName, userIdOf } from "./support/auth";

// What these pages show is a refusal, the same for any Club, so there is nothing to review.
test.use({ disableAutoSnapshot: true });

// Real API only: the mock's Leagues all belong to its one shared Club.
test("a manager of one Club can't open another Club's League, whether or not they can see it", async ({
  page,
}, testInfo) => {
  test.skip(testInfo.project.name === "mock", "Needs a second Club with managers of its own");
  // Sign-in and several API calls: over the default timeout on a busy real-API run.
  test.slow();
  const email = personaEmail(testInfo, "elsewhere");
  await logIn(page, testInfo, email);

  // A Club of their own to manage, and a League in the suite's Club.
  const own = uniqueName(testInfo, "Zz Elsewhere Tennis Club");
  const club = (await (await asAdmin("/clubs", "POST", { name: own })).json()) as { id: string };
  await asAdmin(`/clubs/${club.id}/managers/${await userIdOf(email)}`, "PUT");
  const league = (await (
    await asAdmin(`/clubs/${process.env.E2E_CLUB_ID}/leagues`, "POST", {
      name: uniqueName(testInfo, "Other Club's League"),
      type: "SinglesMen",
      structure: "Divisional",
      startDate: "2030-12-01",
      endDate: "2031-03-31",
      stageLength: { count: 1, unit: "Months" },
      stageRoll: "Manual",
      scoreFormat: { gamesPerSet: 6, setsPerGame: 1, matchTiebreakDecider: false },
    })
  ).json()) as { id: string };

  // Manage is theirs, with their Club alone.
  await page.goto("/manage");
  await expect(page.getByRole("heading", { name: own, level: 2, exact: true })).toBeVisible();
  await expect(page.getByRole("region", { name: "Join link" })).toHaveCount(1);

  // A stranger to that Club: the League isn't there (the API answers 404).
  await page.goto(`/manage/leagues/${league.id}`);
  await expect(page.getByRole("heading", { name: "Page not found" })).toBeVisible();

  // In that Club as a Player, they can see it but not run it (403): the page says the same.
  await asAdmin(`/clubs/${process.env.E2E_CLUB_ID}/players`, "POST", {
    name: uniqueName(testInfo, "Elsie Where"),
    email,
  });
  for (const path of ["", "/start", "/close"]) {
    await page.goto(`/manage/leagues/${league.id}${path}`);
    await expect(page.getByRole("heading", { name: "Page not found" }), `${path || "/"}`).toBeVisible();
  }
});
