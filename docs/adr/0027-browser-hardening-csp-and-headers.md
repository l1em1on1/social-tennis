---
status: accepted
---

# Browser hardening: a nonce-based CSP and static security headers

The web app sends a Content-Security-Policy and a set of security headers on every page (#53). This records the choices behind them. The headers themselves are listed in `docs/architecture.md`, "Authentication".

- **A nonce-based policy, as in Next.js's CSP guide**, with a fresh nonce per request and `'strict-dynamic'`. Next adds the nonce to its scripts only while rendering, so every page renders per request.
- **HSTS without `includeSubDomains`.** Other names on the domain aren't the app's.
- **The verify page sends no referrer.** Its address carries the sign-in token.
- **Ids from forms and the URL must be UUIDs before they go into an API path.** Anything else, such as `..`, could change which API route is called.

## Testing it

- **The injection test turns off Chrome's local-network check for its own file** (#135). A page Playwright serves itself counts as public, so Chrome would otherwise refuse its requests to the container's private address.
- **The injection test takes no Chromatic snapshot** (#155). The page it rewrites in flight didn't record the same way twice.
