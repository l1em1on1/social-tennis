import { http, HttpResponse } from "msw";

import { API_PATHS } from "@/lib/api-paths";
import type { Player } from "@/models";

import { mockPlayers } from "../fixtures";

import type { PlayerStats } from "@/services/types";

let players = [...mockPlayers];

export const playerHandlers = [
  http.get(API_PATHS.players, () => {
    return HttpResponse.json(players);
  }),

  http.get(API_PATHS.player(":id"), ({ params }) => {
    const player = players.find((p) => p.id === params.id);
    if (!player) return new HttpResponse(null, { status: 404 });
    return HttpResponse.json(player);
  }),

  http.post(API_PATHS.players, async ({ request }) => {
    const body = (await request.json()) as Omit<Player, "id" | "score">;
    const player: Player = {
      ...body,
      id: `player-${Date.now()}`,
      score: 0,
    };
    players = [...players, player];
    return HttpResponse.json(player, { status: 201 });
  }),

  http.patch(API_PATHS.player(":id"), async ({ params, request }) => {
    const body = (await request.json()) as Partial<Pick<Player, "name" | "gender" | "level">>;
    const index = players.findIndex((p) => p.id === params.id);
    if (index === -1) return new HttpResponse(null, { status: 404 });
    const updated = { ...players[index], ...body };
    players = players.map((p) => (p.id === params.id ? updated : p));
    return HttpResponse.json(updated);
  }),

  http.delete(API_PATHS.player(":id"), ({ params }) => {
    players = players.filter((p) => p.id !== params.id);
    return new HttpResponse(null, { status: 204 });
  }),

  http.get(API_PATHS.playerStats(":id"), ({ params }) => {
    const player = players.find((p) => p.id === params.id);
    if (!player) return new HttpResponse(null, { status: 404 });
    const stats: PlayerStats = {
      playerId: params.id as string,
      totalGames: 0,
      wins: 0,
      losses: 0,
    };
    return HttpResponse.json(stats);
  }),
];
