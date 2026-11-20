// Chromatic's test (e2e/support/chromatic.ts), with every snapshot turned off below:
// Storybook reviews the primitives (#182). Otherwise identical to @playwright/test.
import { expect, test } from "./support/chromatic";
import { logIn, personaEmail } from "./support/auth";
import { expectNoSeriousA11yViolations } from "./support/axe";

/**
 * The showcase renders every primitive and status badge; checking it with axe
 * in both colour schemes is what holds the design tokens to WCAG AA contrast.
 * Dev-only route, so it exists only in the mocked build.
 */
for (const colorScheme of ["light", "dark"] as const) {
  test.describe(`${colorScheme} mode`, () => {
    // No snapshots: Storybook reviews every primitive, in light and dark
    // (#182). A describe-level option also overrides the real project's
    // opt-out, so the auto snapshot stays off for both projects.
    test.use({ disableAutoSnapshot: true });

    test(`every primitive passes axe in ${colorScheme} mode`, async ({ page }, testInfo) => {
      test.skip(testInfo.project.name !== "mock", "the showcase is compiled out of real production builds");
      await page.emulateMedia({ colorScheme });

      await logIn(page, testInfo, personaEmail(testInfo, "player"));
      await page.goto("/dev/ui");
      await expect(page.getByRole("heading", { name: "UI showcase" })).toBeVisible();
      await expectNoSeriousA11yViolations(page);

      await page.getByRole("button", { name: "Open sheet" }).click();
      await expect(page.getByRole("dialog", { name: "Example sheet" })).toBeVisible();
      await expectNoSeriousA11yViolations(page);
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
