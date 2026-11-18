import type { StorybookConfig } from "@storybook/nextjs-vite";

/**
 * Stories for the visual components (#46, ADR-0015 amendment): each one in
 * light and dark at the 360px phone width (modes.ts), published to Chromatic's
 * Storybook project. Runs in the `storybook` compose service (ADR-0005).
 */
const config: StorybookConfig = {
  framework: "@storybook/nextjs-vite",
  stories: ["../src/**/*.stories.tsx"],
  staticDirs: ["../public"],
  core: { disableTelemetry: true },
};

export default config;
