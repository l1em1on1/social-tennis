import type { Game, League } from "@/models";

import type { LeagueService } from "./types";

export const createMockLeagueService = (initialLeagues: readonly League[]): LeagueService => {
  let leagues = [...initialLeagues];

  return {
    getAll: async () => leagues,

    getById: async (id) => leagues.find((l) => l.id === id) ?? null,

    create: async (data) => {
      const league: League = {
        ...data,
        id: `league-${Date.now()}`,
        stages: [],
        playerIds: [],
      };
      leagues = [...leagues, league];
      return league;
    },

    update: async (id, data) => {
      const index = leagues.findIndex((l) => l.id === id);
      if (index === -1) throw new Error(`League ${id} not found`);
      const updated = { ...leagues[index], ...data };
      leagues = leagues.map((l) => (l.id === id ? updated : l));
      return updated;
    },

    remove: async (id) => {
      leagues = leagues.filter((l) => l.id !== id);
    },

    addPlayer: async (leagueId, playerId) => {
      const index = leagues.findIndex((l) => l.id === leagueId);
      if (index === -1) throw new Error(`League ${leagueId} not found`);
      const league = leagues[index];
      if (league.playerIds.includes(playerId)) return league;
      const updated = { ...league, playerIds: [...league.playerIds, playerId] };
      leagues = leagues.map((l) => (l.id === leagueId ? updated : l));
      return updated;
    },

    removePlayer: async (leagueId, playerId) => {
      const index = leagues.findIndex((l) => l.id === leagueId);
      if (index === -1) throw new Error(`League ${leagueId} not found`);
      const league = leagues[index];
      const updated = {
        ...league,
        playerIds: league.playerIds.filter((pid) => pid !== playerId),
      };
      leagues = leagues.map((l) => (l.id === leagueId ? updated : l));
      return updated;
    },

    getPlayerGames: async (leagueId, playerId) => {
      const league = leagues.find((l) => l.id === leagueId);
      if (!league) return [];
      const allGames = league.stages.flatMap((s) => s.games);
      return allGames.filter(
        (g) =>
          g.playerA1Id === playerId ||
          g.playerA2Id === playerId ||
          g.playerB1Id === playerId ||
          g.playerB2Id === playerId,
      );
    },

    getGameById: async (leagueId, gameId) => {
      const league = leagues.find((l) => l.id === leagueId);
      if (!league) return null;
      const allGames: readonly Game[] = league.stages.flatMap((s) => s.games);
      return allGames.find((g) => g.id === gameId) ?? null;
    },
  };
};
