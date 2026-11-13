import type { ChromaticConfig } from "@chromatic-com/playwright";
import { defineConfig } from "@playwright/test";

/**
 * Frontend E2E seam (ADR-0014). Runs inside the `e2e` compose service against
 * two production builds of the web app:
 *
 *   mock — web-mock, API mocked in-process with MSW (ADR-0009); no api service
 *   real — web-e2e, the real api over the real Postgres
 *
 * Every spec runs in both projects unless it opts out with `test.skip`.
 *
 * Visual review (ADR-0015): specs import `test` from @chromatic-com/playwright,
 * which writes a DOM archive of each test's final page (plus any explicit
 * `takeSnapshot`) to test-results/chromatic-archives; `npx chromatic
 * --playwright` uploads them. Only the mock project archives: its data is
 * fixed, while the real project's Users and timestamps differ every run.
 * Phones first: a 360px-wide touch viewport, the narrowest the shell supports.
 */
const mockUrl = process.env.E2E_MOCK_URL ?? "http://web-mock:3000";
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
  },
  projects: [
    { name: "mock", use: { browserName: "chromium", baseURL: mockUrl } },
    {
      name: "real",
      // Own output dir: Chromatic writes one `<test title>.stories.json` per
      // test under <outputDir>/chromatic-archives, so a shared dir lets this
      // project's empty lists overwrite the mock project's snapshots.
      outputDir: "test-results/real",
      use: { browserName: "chromium", baseURL: realUrl, disableAutoSnapshot: true },
    },
  ],
});
