import type { AuthService, AuthUser } from "./types";

export const createMockAuthService = (): AuthService => {
  let currentUser: AuthUser | null = null;

  return {
    login: async (identifier) => {
      const user: AuthUser = {
        id: `user-${Date.now()}`,
        name: identifier,
        playerId: "player-1",
      };
      currentUser = user;
      return user;
    },

    register: async (name, _identifier) => {
      const user: AuthUser = {
        id: `user-${Date.now()}`,
        name,
        playerId: `player-${Date.now()}`,
      };
      currentUser = user;
      return user;
    },

    logout: async () => {
      currentUser = null;
    },

    getCurrentUser: async () => currentUser,
  };
};
