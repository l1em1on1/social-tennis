import type { ChromaticConfig } from "@chromatic-com/playwright";
import { defineConfig } from "@playwright/test";

/**
 * Frontend E2E seam (ADR-0014). Runs inside the `e2e` compose service against
 * the production build of the web app, in two projects; only its API_URL differs (ADR-0033):
 *
 *   mock — web-mock, with the mock API (mock-api/) behind it: fixed data, read
 *          only (#234). It runs the screens' specs, which only look.
 *   real — web-e2e, the real api over the real Postgres. It runs every other
 *          spec: what the app does is tested here.
 *
 * Visual review (ADR-0015): specs import `test`, `expect` and `takeSnapshot`
 * from e2e/support/chromatic.ts, which wraps @chromatic-com/playwright's and
 * keeps the bottom nav off a long page's content (#182). It writes a DOM
 * archive of each test's final page (plus any explicit `takeSnapshot`) to
 * test-results/chromatic-archives; `npx chromatic
 * --playwright` uploads them. Only the mock project archives: its data is
 * fixed, while the real project's Users and timestamps differ every run.
 * Phones first: a 360px-wide touch viewport, the narrowest the shell supports.
 */
const mockUrl = process.env.E2E_MOCK_URL ?? "http://web-mock:3000";

/** The specs that only look at the fixed data (#234): the mock project's, and Chromatic's. */
const SCREENS = /(screens|manager-pages)\.spec\.ts$/;
const realUrl = process.env.E2E_REAL_URL ?? "http://web-e2e:3000";

export default defineConfig<ChromaticConfig>({
  testDir: "./e2e",
  globalSetup: "./e2e/support/global-setup.ts",
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: [["list"], ["html", { open: "never", outputFolder: "playwright-report" }]],
  use: {
    viewport: { width: 360, height: 780 },
    isMobile: true,
    hasTouch: true,
    trace: "retain-on-failure",
    // British, as the Club is (#275). `locale` sets the page's language and dates, but a
    // native date or time box follows the browser's own language: hence the flag.
    locale: "en-GB",
    timezoneId: "Europe/London",
    launchOptions: { args: ["--lang=en-GB"], env: { LANGUAGE: "en_GB" } },
  },
  projects: [
    { name: "mock", testMatch: SCREENS, use: { browserName: "chromium", baseURL: mockUrl } },
    {
      name: "real",
      testIgnore: SCREENS,
      // Own output dir: Chromatic writes one `<test title>.stories.json` per
      // test under <outputDir>/chromatic-archives, so a shared dir lets this
      // project's empty lists overwrite the mock project's snapshots.
      outputDir: "test-results/real",
      use: { browserName: "chromium", baseURL: realUrl, disableAutoSnapshot: true },
    },
  ],
});
