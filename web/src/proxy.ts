import { NextResponse, type NextRequest } from "next/server";
import { JOIN_CODE_HEADER, joinCodeIn, loginPath } from "@/lib/join";
import { STRICT_TRANSPORT_SECURITY, contentSecurityPolicy, newNonce } from "@/lib/security-headers";
import { publicUrl, secureCookies, sessionCookie } from "@/lib/session";

/** Paths an anonymous visitor may open: the auth surface, and the health check (#111). */
const PUBLIC_PATH = /^\/(login|auth\/verify|health|privacy)(\/|$)/;

/**
 * Runs on every page request (Next 16's proxy convention, the renamed
 * middleware). Two jobs:
 *
 * 1. Security headers (#53): a fresh CSP nonce per request, set on the request
 *    so Next puts it on its own scripts while rendering, and on the response
 *    with the policy. HSTS when served over https. The static headers are in
 *    next.config.ts.
 * 2. Route gate: anonymous visitors are redirected to /login everywhere except
 *    the auth surface, /health and the privacy notice (#51). Presence-only
 *    check: session validity is enforced by
 *    the API on every call, and pages redirect on 401.
 */
export function proxy(request: NextRequest) {
  const https = secureCookies();
  const nonce = newNonce();
  const csp = contentSecurityPolicy(nonce, { https, dev: process.env.NODE_ENV === "development" });

  let response: NextResponse;
  if (!PUBLIC_PATH.test(request.nextUrl.pathname) && !request.cookies.get(sessionCookie())) {
    // From a Club join link (#222), with its code, so the emailed sign-in link brings them back to it.
    response = new NextResponse(null, { status: 307, headers: { Location: publicUrl(loginPath(joinCodeIn(request.nextUrl.pathname))) } });
  } else {
    const requestHeaders = new Headers(request.headers);
    requestHeaders.set("x-nonce", nonce);
    // The join link this request is for, if any, for a page that finds the session revoked (#222).
    requestHeaders.delete(JOIN_CODE_HEADER);
    const join = joinCodeIn(request.nextUrl.pathname);
    if (join) {
      requestHeaders.set(JOIN_CODE_HEADER, join);
    }
    requestHeaders.set("Content-Security-Policy", csp);
    response = NextResponse.next({ request: { headers: requestHeaders } });
  }

  response.headers.set("Content-Security-Policy", csp);
  if (https) {
    response.headers.set("Strict-Transport-Security", STRICT_TRANSPORT_SECURITY);
  }
  return response;
}

export const config = {
  // Everything but static files, which carry no script and get next.config.ts's
  // static headers. Prefetches still pass through: the route gate must see them.
  matcher: ["/((?!_next/static|_next/image|icon.svg).*)"],
};
