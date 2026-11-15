/**
 * The Content-Security-Policy and the other per-request security headers
 * (#53). Dependency-free, because proxy.ts imports it and Next's proxy must
 * not import server-only modules.
 *
 * The CSP follows Next's own guide (node_modules/next/dist/docs/01-app/
 * 02-guides/content-security-policy.md): a fresh nonce per request, set by
 * the proxy on both the request (Next reads it while rendering and puts it on
 * its own scripts) and the response. That needs every page rendered per
 * request, which the root layout enforces.
 */

/** A fresh nonce for one response, as in Next's CSP guide. */
export const newNonce = () => Buffer.from(crypto.randomUUID()).toString("base64");

/**
 * The policy for one response.
 *
 * - `https`: the app is served over https (PUBLIC_BASE_URL). Only then is
 *   `upgrade-insecure-requests` sent: on the plain-http compose network it
 *   would rewrite the app's own requests to an https that isn't there.
 * - `dev`: `next dev`, where React needs `eval` for its debugging aids and
 *   dev-only inline styles carry no nonce (the guide's development variant).
 */
export function contentSecurityPolicy(nonce: string, { https, dev }: { https: boolean; dev: boolean }): string {
  return [
    "default-src 'self'",
    `script-src 'self' 'nonce-${nonce}' 'strict-dynamic'${dev ? " 'unsafe-eval'" : ""}`,
    `style-src 'self' ${dev ? "'unsafe-inline'" : `'nonce-${nonce}'`}`,
    "img-src 'self' blob: data:",
    "font-src 'self'",
    "connect-src 'self'",
    "object-src 'none'",
    "base-uri 'self'",
    "form-action 'self'",
    "frame-ancestors 'none'",
    ...(https ? ["upgrade-insecure-requests"] : []),
  ].join("; ");
}

/**
 * Strict-Transport-Security, only over https: browsers ignore it on http, and
 * on localhost it would pin a name other local apps use. One year, no
 * `includeSubDomains` or preload: other names on the domain (mail records) are
 * not this app's to commit to https.
 */
export const STRICT_TRANSPORT_SECURITY = "max-age=31536000";
