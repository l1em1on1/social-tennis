import type { LeagueType } from "./enums";
import type { Game } from "./game";

export interface Stage {
  readonly id: string;
  readonly startDate: string;
  readonly endDate: string;
  readonly games: readonly Game[];
}

export interface League {
  readonly id: string;
  readonly name: string;
  readonly type: LeagueType;
  readonly stages: readonly Stage[];
  readonly playerIds: readonly string[];
}
