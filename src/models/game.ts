export interface Reaction {
  readonly id: string;
  readonly approved: boolean;
  readonly comment: string;
}

export interface Score {
  readonly id: string;
  readonly playerId: string;
  readonly scoreA: number;
  readonly scoreB: number;
  readonly date: string;
  readonly reactions: readonly Reaction[];
}

export interface Game {
  readonly id: string;
  readonly playerA1Id: string;
  readonly playerA2Id: string | null;
  readonly playerB1Id: string;
  readonly playerB2Id: string | null;
  readonly scores: readonly Score[];
  readonly date: string | null;
  readonly court: string | null;
}
