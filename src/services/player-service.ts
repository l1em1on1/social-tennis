import type { Player } from "@/models";

import type { PlayerService, PlayerStats } from "./types";

export const createMockPlayerService = (initialPlayers: readonly Player[]): PlayerService => {
  let players = [...initialPlayers];

  return {
    getAll: async () => players,

    getById: async (id) => players.find((p) => p.id === id) ?? null,

    create: async (data) => {
      const player: Player = {
        ...data,
        id: `player-${Date.now()}`,
        score: 0,
      };
      players = [...players, player];
      return player;
    },

    update: async (id, data) => {
      const index = players.findIndex((p) => p.id === id);
      if (index === -1) throw new Error(`Player ${id} not found`);
      const updated = { ...players[index], ...data };
      players = players.map((p) => (p.id === id ? updated : p));
      return updated;
    },

    remove: async (id) => {
      players = players.filter((p) => p.id !== id);
    },

    getStats: async (id) => {
      const player = players.find((p) => p.id === id);
      if (!player) return null;
      const stats: PlayerStats = {
        playerId: id,
        totalGames: 0,
        wins: 0,
        losses: 0,
      };
      return stats;
    },
  };
};
