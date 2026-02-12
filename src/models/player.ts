import type { Gender, PlayerLevel } from "./enums";

export interface Player {
  readonly id: string;
  readonly name: string;
  readonly gender: Gender;
  readonly level: PlayerLevel;
  readonly score: number;
}
