import type { NextConfig } from "next";

/**
 * Security headers that are the same on every response, static files included
 * (#53). The Content-Security-Policy and HSTS vary per request and are set in
 * src/proxy.ts.
 */
const staticSecurityHeaders = [
  { key: "X-Content-Type-Options", value: "nosniff" },
  // Nothing about a page's address leaves the site. The sign-in page sets
  // no-referrer as well: its address is the credential until submitted.
  { key: "Referrer-Policy", value: "same-origin" },
  // For browsers without CSP frame-ancestors.
  { key: "X-Frame-Options", value: "DENY" },
  { key: "Cross-Origin-Opener-Policy", value: "same-origin" },
  { key: "Permissions-Policy", value: "camera=(), microphone=(), geolocation=(), payment=(), usb=()" },
];

const nextConfig: NextConfig = {
  // Self-contained server bundle for the Docker runtime image (web/Dockerfile).
  output: "standalone",

  // No `X-Powered-By: Next.js` (#53).
  poweredByHeader: false,

  async headers() {
    return [{ source: "/:path*", headers: staticSecurityHeaders }];
  },
};

export default nextConfig;
