import { defineConfig } from "@playwright/test";

/** PROTOTYPE (#254), throwaway: photographs the app in each look. Not for main. */
export default defineConfig({
  testDir: "./e2e/looks",
  testMatch: /\.looks\.ts$/,
  outputDir: "/tmp/looks-results",
  reporter: [["list"]],
  timeout: 900_000,
  expect: { timeout: 15_000 },
  use: {
    baseURL: process.env.E2E_REAL_URL ?? "http://web-e2e:3000",
    viewport: { width: 360, height: 780 },
    deviceScaleFactor: 2,
    isMobile: true,
    hasTouch: true,
    locale: "en-GB",
    // The looks' stylesheet is injected, which the app's Content-Security-Policy refuses.
    bypassCSP: true,
    launchOptions: { args: ["--lang=en-GB"], env: { LANGUAGE: "en_GB" } },
  },
});
