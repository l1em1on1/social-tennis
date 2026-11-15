/** Header the API reads the browser's address from (the API's ClientAddress.HeaderName). */
export const CLIENT_ADDRESS_HEADER = "X-Client-Address";

/**
 * The browser's address, from the request's X-Forwarded-For, for the API's
 * per-client sign-in limit (#108).
 *
 * Only the right-most entry is trusted. Azure Container Apps' ingress appends
 * the address it saw to whatever the client sent, and "only the rightmost IP is
 * provided by Azure Container Apps" (Container Apps ingress docs, HTTP
 * headers). Entries before it came from the client, so taking any of them
 * would let a browser choose its own bucket.
 *
 * Locally there is no ingress: Next sets the header to the connection's
 * address only when the request has none (base-server.js), so a local client
 * could still set it. Nothing local is exposed to the internet.
 */
export function clientAddress(forwardedFor: string | null): string | undefined {
  const rightMost = forwardedFor?.split(",").at(-1)?.trim();
  return rightMost || undefined;
}
