// Chromatic's test captures a DOM snapshot at the end of every test (mock project only;
// see playwright.config.ts). Otherwise identical to @playwright/test.
import { expect, takeSnapshot, test } from "@chromatic-com/playwright";
import { logIn, personaEmail } from "./support/auth";
import { expectNoSeriousA11yViolations } from "./support/axe";

/**
 * The showcase renders every primitive and status badge; checking it with axe
 * in both colour schemes is what holds the design tokens to WCAG AA contrast.
 * Dev-only route, so it exists only in the mocked build.
 */
for (const colorScheme of ["light", "dark"] as const) {
  test.describe(`${colorScheme} mode`, () => {
    // Explicit snapshots only, light mode only. Chromatic documents
    // colour-scheme modes for Storybook alone; a Playwright archive replays
    // without our emulateMedia, so a "dark" snapshot would be a light
    // duplicate. Dark stays covered by axe here (and by Storybook modes,
    // later). A describe-level option also overrides the real project's
    // opt-out, so the auto snapshot stays off for both projects.
    test.use({ disableAutoSnapshot: true });

    test(`every primitive passes axe in ${colorScheme} mode`, async ({ page }, testInfo) => {
      test.skip(testInfo.project.name !== "mock", "the showcase is compiled out of real production builds");
      await page.emulateMedia({ colorScheme });

      await logIn(page, testInfo, personaEmail(testInfo, "player"));
      await page.goto("/dev/ui");
      await expect(page.getByRole("heading", { name: "UI showcase" })).toBeVisible();
      await expectNoSeriousA11yViolations(page);
      if (colorScheme === "light") await takeSnapshot(page, "UI showcase", testInfo);

      await page.getByRole("button", { name: "Open sheet" }).click();
      await expect(page.getByRole("dialog", { name: "Example sheet" })).toBeVisible();
      await expectNoSeriousA11yViolations(page);
      if (colorScheme === "light") await takeSnapshot(page, "UI showcase, sheet open", testInfo);
    });
  });
}

test.describe("real build", () => {
  // Skipped in the mock project, where an auto snapshot would be a blank page.
  test.use({ disableAutoSnapshot: true });

  test("the showcase is not served by a real production build", async ({ page }, testInfo) => {
    test.skip(testInfo.project.name !== "real", "only the real build compiles it out");
    await logIn(page, testInfo, personaEmail(testInfo, "player"));
    await page.goto("/dev/ui");
    await expect(page.getByRole("heading", { name: "Page not found" })).toBeVisible();
  });
});
