/**
 * True when the API is mocked with MSW (ADR-0009). `MOCK_API` is inlined at
 * build time by `env` in next.config.ts, so in a normal production build this
 * is the constant `false` and everything it guards is dropped from the bundle.
 */
export const mockApi = process.env.MOCK_API === "1";

/** Dev-only surfaces (the UI showcase): in `next dev`, or in a mocked build. */
export const devSurfacesEnabled = process.env.NODE_ENV !== "production" || mockApi;
