import { defineConfig } from "@playwright/test";

/**
 * Storybook's accessibility check (ADR-0014, #232): every story of the built
 * Storybook, in light and dark, at the phone width its snapshots use. Runs in
 * the `storybook` compose service, after `npm run build-storybook`:
 *
 *   docker compose --profile storybook run --rm --no-deps storybook npm run test-storybook
 *
 * A config of its own, not a project of playwright.config.ts: that one needs
 * the e2e stack up, and this one needs no app at all.
 */
const port = 6007;

export default defineConfig({
  testDir: "./storybook-tests",
  outputDir: "test-results/storybook",
  fullyParallel: true,
  forbidOnly: true,
  reporter: [["list"]],
  use: {
    baseURL: `http://127.0.0.1:${port}`,
    viewport: { width: 360, height: 780 },
    // British, as the Club is (#275). `locale` sets the page's language and dates, but a
    // native date or time box follows the browser's own language: hence the flag.
    locale: "en-GB",
    timezoneId: "Europe/London",
    launchOptions: { args: ["--lang=en-GB"], env: { LANGUAGE: "en_GB" } },
  },
  webServer: {
    // Serves the build as it is (Vite docs, `vite preview`).
    command: `npx vite preview --outDir storybook-static --host 127.0.0.1 --port ${port} --strictPort`,
    url: `http://127.0.0.1:${port}/iframe.html`,
    reuseExistingServer: false,
  },
});
