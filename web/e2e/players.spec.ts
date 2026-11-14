// Chromatic's test captures a DOM snapshot at the end of every test (mock project only;
// see playwright.config.ts). Otherwise identical to @playwright/test.
import { expect, test } from "@chromatic-com/playwright";
import { addFirstPlayer, logIn, personaEmail } from "./support/auth";
import { expectNoSeriousA11yViolations } from "./support/axe";

test("a first sign-in asks for a name, and the name can be changed in Profiles", async ({ page }, testInfo) => {
  await logIn(page, testInfo, personaEmail(testInfo, "player"));

  // Nobody to play as yet: no "Playing as", and the question is on Home.
  await expect(page.getByTestId("acting-player")).toHaveCount(0);
  await expect(page.getByRole("heading", { name: "Welcome! What's your name?" })).toBeVisible();
  await expectNoSeriousA11yViolations(page);

  await addFirstPlayer(page, "Sam Smith");
  await expect(page.getByRole("heading", { name: "Welcome! What's your name?" })).toHaveCount(0);

  await page.getByRole("navigation", { name: "Main" }).getByRole("link", { name: "Profiles" }).click();
  await page.getByRole("button", { name: "Rename Sam Smith" }).click();
  const sheet = page.getByRole("dialog", { name: "Rename Sam Smith" });
  await expectNoSeriousA11yViolations(page);
  await sheet.getByLabel("Name").fill("Samantha Smith");
  await sheet.getByRole("button", { name: "Save name" }).click();

  await expect(page.getByTestId("acting-player")).toHaveText("Samantha Smith");
  await expect(page.getByRole("dialog")).toHaveCount(0);

  // It is stored, not just shown.
  await page.reload();
  await expect(page.getByTestId("acting-player")).toHaveText("Samantha Smith");
});

test("a name of only spaces is refused with a message", async ({ page }, testInfo) => {
  await logIn(page, testInfo, personaEmail(testInfo, "player"));
  await page.getByLabel("Your name").fill("   ");
  await page.getByRole("button", { name: "Save my name" }).click();

  await expect(page.getByText("That name didn't work")).toBeVisible();
  await expect(page.getByTestId("acting-player")).toHaveCount(0);
});

test("a Player joins a Club from Home", async ({ page }, testInfo) => {
  await logIn(page, testInfo, personaEmail(testInfo, "player"));

  // No Player, nobody to join as.
  await expect(page.getByRole("button", { name: /^Join / })).toHaveCount(0);
  await addFirstPlayer(page, "Sam Smith");

  const club = page.getByRole("listitem").filter({ has: page.getByRole("button", { name: /^Join / }) }).first();
  const clubName = (await club.locator("span").first().textContent()) ?? "";
  await page.getByRole("button", { name: `Join ${clubName}` }).click();

  const joined = page.getByRole("listitem").filter({ hasText: clubName });
  await expect(joined).toContainText("Member");
  await expect(page.getByRole("button", { name: `Join ${clubName}` })).toHaveCount(0);

  await page.getByRole("navigation", { name: "Main" }).getByRole("link", { name: "Profiles" }).click();
  await expect(page.getByText(`Member of ${clubName}`)).toBeVisible();
  await expectNoSeriousA11yViolations(page);
});
