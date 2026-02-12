import type { Game, League, Player, Social, Vote } from "@/models";

export interface PlayerStats {
  readonly playerId: string;
  readonly totalGames: number;
  readonly wins: number;
  readonly losses: number;
}

export interface AuthUser {
  readonly id: string;
  readonly name: string;
  readonly playerId: string;
}

export interface LeagueService {
  getAll(): Promise<readonly League[]>;
  getById(id: string): Promise<League | null>;
  create(league: Omit<League, "id" | "stages" | "playerIds">): Promise<League>;
  update(id: string, data: Partial<Pick<League, "name" | "type">>): Promise<League>;
  remove(id: string): Promise<void>;
  addPlayer(leagueId: string, playerId: string): Promise<League>;
  removePlayer(leagueId: string, playerId: string): Promise<League>;
  getPlayerGames(leagueId: string, playerId: string): Promise<readonly Game[]>;
  getGameById(leagueId: string, gameId: string): Promise<Game | null>;
}

export interface SocialService {
  getAll(): Promise<readonly Social[]>;
  getById(id: string): Promise<Social | null>;
  create(social: Omit<Social, "id" | "playerIds" | "votes" | "games">): Promise<Social>;
  update(id: string, data: Partial<Pick<Social, "name" | "date">>): Promise<Social>;
  remove(id: string): Promise<void>;
  addVote(socialId: string, vote: Omit<Vote, "id">): Promise<Social>;
  removeVote(socialId: string, voteId: string): Promise<Social>;
}

export interface PlayerService {
  getAll(): Promise<readonly Player[]>;
  getById(id: string): Promise<Player | null>;
  create(player: Omit<Player, "id" | "score">): Promise<Player>;
  update(id: string, data: Partial<Pick<Player, "name" | "gender" | "level">>): Promise<Player>;
  remove(id: string): Promise<void>;
  getStats(id: string): Promise<PlayerStats | null>;
}

export interface AuthService {
  login(identifier: string): Promise<AuthUser>;
  register(name: string, identifier: string): Promise<AuthUser>;
  logout(): Promise<void>;
  getCurrentUser(): Promise<AuthUser | null>;
}

export interface Services {
  readonly league: LeagueService;
  readonly social: SocialService;
  readonly player: PlayerService;
  readonly auth: AuthService;
}
