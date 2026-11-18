/**
 * Chromatic modes: the 360px phone width the app is designed for, in each
 * colour scheme. `colorScheme` sets prefers-color-scheme, which is what swaps
 * the tokens in globals.css (Chromatic docs, "Browser options").
 */
export const allModes = {
  light: { viewport: 360, colorScheme: "light" },
  dark: { viewport: 360, colorScheme: "dark" },
} as const;
