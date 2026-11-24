import { readFileSync } from "node:fs";
import path from "node:path";
import { expect, test, type Page } from "@playwright/test";
import { expectNoSeriousA11yViolations } from "../e2e/support/axe";

/**
 * Every story, in both colour schemes: its play runs to the end, then axe
 * finds nothing serious. The plays matter here: one that opens a sheet or a
 * list leaves it open to be checked.
 *
 * This is what holds the design tokens to WCAG AA contrast (ADR-0014).
 */
interface StoryIndex {
  entries: Record<string, { id: string; type: "story" | "docs"; title: string; name: string }>;
}

const index = JSON.parse(
  readFileSync(path.join(__dirname, "../storybook-static/index.json"), "utf8"),
) as StoryIndex;
const stories = Object.values(index.entries).filter((entry) => entry.type === "story");

/** What a story reported on Storybook's channel while it rendered. */
interface StoryRun {
  finished?: { storyId: string; status: "success" | "error" };
  problems: string[];
}

declare global {
  interface Window {
    storyRun: StoryRun;
  }
}

/**
 * Listens from the moment Storybook creates its channel, so a story that
 * finishes at once isn't missed. `storyFinished` comes after the play and the
 * animations; the others say why a story failed.
 */
async function recordStoryRun(page: Page) {
  await page.addInitScript(() => {
    window.storyRun = { problems: [] };
    const key = "__STORYBOOK_ADDONS_CHANNEL__";
    Object.defineProperty(window, key, {
      configurable: true,
      get: () => undefined,
      set: (channel: { on(event: string, listener: (payload: never) => void): void }) => {
        Object.defineProperty(window, key, { value: channel, writable: true, configurable: true });
        channel.on("storyFinished", (payload: StoryRun["finished"]) => {
          window.storyRun.finished = payload;
        });
        for (const event of [
          "playFunctionThrewException",
          "unhandledErrorsWhilePlaying",
          "storyThrewException",
          "storyErrored",
          "storyMissing",
        ]) {
          channel.on(event, (payload: unknown) => {
            window.storyRun.problems.push(`${event}: ${JSON.stringify(payload)}`);
          });
        }
      },
    });
  });
}

test("the built Storybook has stories", () => {
  expect(stories.length).toBeGreaterThan(0);
});

for (const colorScheme of ["light", "dark"] as const) {
  test.describe(colorScheme, () => {
    test.use({ colorScheme });

    for (const story of stories) {
      test(`${story.title}: ${story.name}`, async ({ page }) => {
        await recordStoryRun(page);
        await page.goto(`/iframe.html?id=${story.id}&viewMode=story`);
        await page.waitForFunction(() => window.storyRun.finished ?? window.storyRun.problems.length > 0);

        const { finished, problems } = await page.evaluate(() => window.storyRun);
        expect(problems).toEqual([]);
        expect(finished).toMatchObject({ storyId: story.id, status: "success" });

        await expectNoSeriousA11yViolations(page);
      });
    }
  });
}
