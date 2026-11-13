import AxeBuilder from "@axe-core/playwright";
import { expect, type Page } from "@playwright/test";

/**
 * Fails on any serious or critical WCAG 2.1 A/AA violation on the current
 * page. Minor and moderate findings are reported by axe but not gating.
 */
export async function expectNoSeriousA11yViolations(page: Page) {
  const { violations } = await new AxeBuilder({ page })
    .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"])
    .analyze();

  const serious = violations
    .filter((violation) => violation.impact === "serious" || violation.impact === "critical")
    .map(({ id, impact, help, nodes }) => ({ id, impact, help, targets: nodes.map((node) => node.target.join(" ")) }));

  expect(serious, JSON.stringify(serious, null, 2)).toEqual([]);
}
