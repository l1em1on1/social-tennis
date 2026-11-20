---
status: accepted
---

# Sign-in and sessions: opaque hashed tokens, a link redeemed by a button, limits per client and per address

ADR-0004 chose passwordless email sign-in. This records how it is built (#3, #53, #107, #108), and why. The mechanism is in `docs/architecture.md`, "Authentication".

## Sessions

- **Opaque, revocable session tokens, not JWTs.** There is no signing key to manage. Logging out is a row update, and it takes effect at once.
- **Tokens are stored as SHA-256 hashes**, both sign-in links and sessions. A database leak then leaks nothing anyone can redeem.
- **Redirects use `PUBLIC_BASE_URL`.** A server in a container sees its bind address as its own origin, and a browser can't reach that address.
- **Cookies are `Secure` exactly when `PUBLIC_BASE_URL` is https, not by `NODE_ENV`.** Every image runs as `production`, also over plain http on the compose network, where a browser stores a Secure cookie but never sends it back (only `localhost` is exempt). A TLS deployment has to set an https `PUBLIC_BASE_URL` for its redirects anyway, so it gets Secure cookies.
- **Over https the cookie is `__Host-` prefixed.** The browser then accepts it only if it is Secure, on `Path=/` and has no `Domain`, so no other subdomain can plant one.

## Sign-in links

- **Opening a link redeems nothing.** Mail scanners and link previewers follow GETs, and would use up a single-use token before its owner clicks. The link opens a page, and its "Sign in" button redeems the token with a POST.
- **A User is created when a link is redeemed, not when one is requested** (#108). An address nobody answers for never becomes a User, so it never appears in the Admin's Users list.
- **The log is the default mail provider**, so development needs no mail account. Outside Development the API refuses to start with it unless it is allowed explicitly (#107).
- **A failed send answers 503, and the link is deleted.** The link reached nobody, and it would otherwise count against the address's quota. The Azure client gets one retry and a ten-second timeout, so the form isn't left waiting.

## Limits on requesting a link (#108)

- **The client is the browser's address, as the BFF saw it.** The BFF takes the right-most `X-Forwarded-For` entry. On Container Apps that is the entry the ingress appended ("only the rightmost IP is provided by Azure Container Apps"), so a browser that sends its own header still lands in its real bucket. The BFF passes it as `X-Client-Address`, and the API never reads `X-Forwarded-For`. The API's ingress is internal, so only apps in the environment can set that header.
- **Limits are kept in memory, per replica.** That is enough at one replica (`max_replicas = 1`, ADR-0019).
- **The ceiling across all clients is checked after the per-client limit**, so a client refused by its own limit doesn't use up the ceiling.
- **An address's quota counts per client and in total.** Counting per client means one client can't use up another's links for the same address. The total stops many clients together from mail-bombing it. A link stores a hash of its client's key, not the client's network address (ADR-0024).
- **The compose stack raises the per-client limit**, because the E2E suite signs in from one container.
