import { NextResponse, type NextRequest } from "next/server";
import { api } from "@/lib/api/client";
import { SESSION_COOKIE, publicUrl } from "@/lib/session";

/**
 * Lands the emailed magic link: redeems the raw token at the API and swaps it
 * for an opaque session credential in an HttpOnly cookie. Invalid, used, or
 * expired links bounce back to /login with a clear message.
 */
const redirectTo = (path: string) =>
  new NextResponse(null, { status: 307, headers: { Location: publicUrl(path) } });

export async function GET(request: NextRequest) {
  const token = request.nextUrl.searchParams.get("token");
  if (!token) {
    return redirectTo("/login?error=invalid-link");
  }

  const { data, response } = await api.POST("/auth/sessions", { body: { token } });
  if (!response.ok || !data) {
    return redirectTo("/login?error=invalid-link");
  }

  const home = redirectTo("/");
  home.cookies.set(SESSION_COOKIE, data.token, {
    httpOnly: true,
    sameSite: "lax",
    secure: process.env.NODE_ENV === "production",
    path: "/",
    expires: new Date(data.expiresAt),
  });
  return home;
}
