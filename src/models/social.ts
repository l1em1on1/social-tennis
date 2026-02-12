import type { Game } from "./game";

export interface Vote {
  readonly id: string;
  readonly playerId: string;
  readonly date: string;
}

export interface Social {
  readonly id: string;
  readonly name: string;
  readonly date: string;
  readonly playerIds: readonly string[];
  readonly votes: readonly Vote[];
  readonly games: readonly Game[];
}
