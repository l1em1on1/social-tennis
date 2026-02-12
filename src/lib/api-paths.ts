export const API_PATHS = {
  leagues: "/api/leagues",
  league: (id: string): string => `/api/leagues/${id}`,
  leaguePlayers: (id: string): string => `/api/leagues/${id}/players`,
  leaguePlayer: (leagueId: string, playerId: string): string =>
    `/api/leagues/${leagueId}/players/${playerId}`,
  leagueGames: (id: string): string => `/api/leagues/${id}/games`,
  leagueGame: (leagueId: string, gameId: string): string =>
    `/api/leagues/${leagueId}/games/${gameId}`,

  socials: "/api/socials",
  social: (id: string): string => `/api/socials/${id}`,
  socialVotes: (id: string): string => `/api/socials/${id}/votes`,
  socialVote: (socialId: string, voteId: string): string =>
    `/api/socials/${socialId}/votes/${voteId}`,

  players: "/api/players",
  player: (id: string): string => `/api/players/${id}`,
  playerStats: (id: string): string => `/api/players/${id}/stats`,

  authLogin: "/api/auth/login",
  authRegister: "/api/auth/register",
  authLogout: "/api/auth/logout",
  authMe: "/api/auth/me",
} as const;
