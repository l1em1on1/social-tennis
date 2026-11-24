/**
 * Chromatic modes: the 360px phone width the app is designed for, in each
 * colour scheme, and a desktop width for what changes from the md breakpoint. `colorScheme` sets prefers-color-scheme, which is what swaps
 * the tokens in globals.css (Chromatic docs, "Browser options").
 */
export const allModes = {
  light: { viewport: 360, colorScheme: "light" },
  dark: { viewport: 360, colorScheme: "dark" },
  /** From the md breakpoint up the shell changes (#225): the stories of what changes add this mode. */
  desktop: { viewport: 1280, colorScheme: "light" },
} as const;
