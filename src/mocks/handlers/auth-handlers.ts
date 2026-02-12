import { http, HttpResponse } from "msw";

import { API_PATHS } from "@/lib/api-paths";

import type { AuthUser } from "@/services/types";

let currentUser: AuthUser | null = null;

export const authHandlers = [
  http.post(API_PATHS.authLogin, async ({ request }) => {
    const { identifier } = (await request.json()) as { identifier: string };
    const user: AuthUser = {
      id: `user-${Date.now()}`,
      name: identifier,
      playerId: "player-1",
    };
    currentUser = user;
    return HttpResponse.json(user);
  }),

  http.post(API_PATHS.authRegister, async ({ request }) => {
    const { name, identifier } = (await request.json()) as {
      name: string;
      identifier: string;
    };
    void identifier;
    const user: AuthUser = {
      id: `user-${Date.now()}`,
      name,
      playerId: `player-${Date.now()}`,
    };
    currentUser = user;
    return HttpResponse.json(user, { status: 201 });
  }),

  http.post(API_PATHS.authLogout, () => {
    currentUser = null;
    return new HttpResponse(null, { status: 204 });
  }),

  http.get(API_PATHS.authMe, () => {
    if (!currentUser) return new HttpResponse(null, { status: 401 });
    return HttpResponse.json(currentUser);
  }),
];
