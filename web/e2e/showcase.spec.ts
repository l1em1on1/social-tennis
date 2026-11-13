import { expect, test } from "@playwright/test";
import { logIn, personaEmail } from "./support/auth";
import { expectNoSeriousA11yViolations } from "./support/axe";

/**
 * The showcase renders every primitive and status badge; checking it with axe
 * in both colour schemes is what holds the design tokens to WCAG AA contrast.
 * Dev-only route, so it exists only in the mocked build.
 */
for (const colorScheme of ["light", "dark"] as const) {
  test(`every primitive passes axe in ${colorScheme} mode`, async ({ page }, testInfo) => {
    test.skip(testInfo.project.name !== "mock", "the showcase is compiled out of real production builds");
    await page.emulateMedia({ colorScheme });

    await logIn(page, testInfo, personaEmail("player"));
    await page.goto("/dev/ui");
    await expect(page.getByRole("heading", { name: "UI showcase" })).toBeVisible();
    await expectNoSeriousA11yViolations(page);

    await page.getByRole("button", { name: "Open sheet" }).click();
    await expect(page.getByRole("dialog", { name: "Example sheet" })).toBeVisible();
    await expectNoSeriousA11yViolations(page);
  });
}

test("the showcase is not served by a real production build", async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== "real", "only the real build compiles it out");
  await logIn(page, testInfo, personaEmail("player"));
  await page.goto("/dev/ui");
  await expect(page.getByRole("heading", { name: "Page not found" })).toBeVisible();
});
