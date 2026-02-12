import { http, HttpResponse } from "msw";

import { API_PATHS } from "@/lib/api-paths";
import type { League } from "@/models";

import { mockLeagues } from "../fixtures";

let leagues = [...mockLeagues];

export const leagueHandlers = [
  http.get(API_PATHS.leagues, () => {
    return HttpResponse.json(leagues);
  }),

  http.get(API_PATHS.league(":id"), ({ params }) => {
    const league = leagues.find((l) => l.id === params.id);
    if (!league) return new HttpResponse(null, { status: 404 });
    return HttpResponse.json(league);
  }),

  http.post(API_PATHS.leagues, async ({ request }) => {
    const body = (await request.json()) as Omit<League, "id" | "stages" | "playerIds">;
    const league: League = {
      ...body,
      id: `league-${Date.now()}`,
      stages: [],
      playerIds: [],
    };
    leagues = [...leagues, league];
    return HttpResponse.json(league, { status: 201 });
  }),

  http.patch(API_PATHS.league(":id"), async ({ params, request }) => {
    const body = (await request.json()) as Partial<Pick<League, "name" | "type">>;
    const index = leagues.findIndex((l) => l.id === params.id);
    if (index === -1) return new HttpResponse(null, { status: 404 });
    const updated = { ...leagues[index], ...body };
    leagues = leagues.map((l) => (l.id === params.id ? updated : l));
    return HttpResponse.json(updated);
  }),

  http.delete(API_PATHS.league(":id"), ({ params }) => {
    leagues = leagues.filter((l) => l.id !== params.id);
    return new HttpResponse(null, { status: 204 });
  }),

  http.post(API_PATHS.leaguePlayers(":id"), async ({ params, request }) => {
    const { playerId } = (await request.json()) as { playerId: string };
    const index = leagues.findIndex((l) => l.id === params.id);
    if (index === -1) return new HttpResponse(null, { status: 404 });
    const league = leagues[index];
    const updated = { ...league, playerIds: [...league.playerIds, playerId] };
    leagues = leagues.map((l) => (l.id === params.id ? updated : l));
    return HttpResponse.json(updated);
  }),

  http.delete(API_PATHS.leaguePlayer(":leagueId", ":playerId"), ({ params }) => {
    const index = leagues.findIndex((l) => l.id === params.leagueId);
    if (index === -1) return new HttpResponse(null, { status: 404 });
    const league = leagues[index];
    const updated = {
      ...league,
      playerIds: league.playerIds.filter((pid) => pid !== params.playerId),
    };
    leagues = leagues.map((l) => (l.id === params.leagueId ? updated : l));
    return HttpResponse.json(updated);
  }),

  http.get(API_PATHS.leagueGames(":id"), ({ params }) => {
    const league = leagues.find((l) => l.id === params.id);
    if (!league) return new HttpResponse(null, { status: 404 });
    const games = league.stages.flatMap((s) => s.games);
    return HttpResponse.json(games);
  }),

  http.get(API_PATHS.leagueGame(":leagueId", ":gameId"), ({ params }) => {
    const league = leagues.find((l) => l.id === params.leagueId);
    if (!league) return new HttpResponse(null, { status: 404 });
    const game = league.stages.flatMap((s) => s.games).find((g) => g.id === params.gameId);
    if (!game) return new HttpResponse(null, { status: 404 });
    return HttpResponse.json(game);
  }),
];
