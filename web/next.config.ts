import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Self-contained server bundle for the Docker runtime image (web/Dockerfile).
  output: "standalone",

  // Inline MOCK_API at build time (Next docs, next.config.js `env`: replaced
  // in the bundle, not read at runtime). A build without it compiles every
  // `process.env.MOCK_API === "1"` to false, so the MSW import in
  // instrumentation.ts is dead code and never bundled. Mocked builds opt in
  // explicitly: `next dev` with MOCK_API=1, or the web-mock image's build arg.
  env: {
    MOCK_API: process.env.MOCK_API ?? "",
  },
};

export default nextConfig;
