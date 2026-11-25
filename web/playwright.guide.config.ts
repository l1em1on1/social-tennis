import { defineConfig } from "@playwright/test";

/**
 * Takes the screenshots of the how-to guides (docs/ui/guide, #251): one story
 * told by the real app and API, over an empty database. Not a test of the app,
 * and not run in CI. How to run it is in docs/ui/README.md.
 *
 * A config of its own: playwright.config.ts runs every spec twice and sets the
 * suite's Club up first, and this needs neither.
 */
export default defineConfig({
  testDir: "./e2e/guide",
  testMatch: /\.guide\.ts$/,
  outputDir: "/tmp/guide-results",
  forbidOnly: true,
  reporter: [["list"]],
  timeout: 300_000,
  expect: { timeout: 10_000 },
  use: {
    baseURL: process.env.E2E_REAL_URL ?? "http://web-e2e:3000",
    viewport: { width: 390, height: 780 },
    // Sharp on a phone, and on a projector.
    deviceScaleFactor: 2,
    isMobile: true,
    hasTouch: true,
    colorScheme: "light",
    locale: "en-GB",
    // Date boxes follow the browser's own language, not the page's locale.
    launchOptions: { args: ["--lang=en-GB"], env: { LANGUAGE: "en_GB" } },
  },
});
