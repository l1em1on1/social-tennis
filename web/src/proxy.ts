import { NextResponse, type NextRequest } from "next/server";
import { SESSION_COOKIE, publicUrl } from "@/lib/session";

/**
 * Route gate (Next 16's proxy convention — the renamed middleware): anonymous
 * visitors are redirected to /login everywhere except the auth surface and
 * static assets. Presence-only check — actual session validity is enforced by
 * the API on every call, and pages redirect on 401.
 */
export function proxy(request: NextRequest) {
  if (!request.cookies.get(SESSION_COOKIE)) {
    return new NextResponse(null, { status: 307, headers: { Location: publicUrl("/login") } });
  }
  return NextResponse.next();
}

export const config = {
  matcher: ["/((?!login|auth/verify|_next/static|_next/image|favicon.ico).*)"],
};
