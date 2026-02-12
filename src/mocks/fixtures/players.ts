import type { Player } from "@/models";
import { Gender, PlayerLevel } from "@/models";

export const mockPlayers: readonly Player[] = [
  {
    id: "player-1",
    name: "James Wilson",
    gender: Gender.Male,
    level: PlayerLevel.Level4,
    score: 1250,
  },
  {
    id: "player-2",
    name: "Sarah Chen",
    gender: Gender.Female,
    level: PlayerLevel.Level3,
    score: 1100,
  },
  {
    id: "player-3",
    name: "Mike Thompson",
    gender: Gender.Male,
    level: PlayerLevel.Level5,
    score: 1400,
  },
  {
    id: "player-4",
    name: "Emma Roberts",
    gender: Gender.Female,
    level: PlayerLevel.Level4,
    score: 1200,
  },
  {
    id: "player-5",
    name: "David Park",
    gender: Gender.Male,
    level: PlayerLevel.Level2,
    score: 900,
  },
  {
    id: "player-6",
    name: "Lisa Martinez",
    gender: Gender.Female,
    level: PlayerLevel.Level3,
    score: 1050,
  },
];
