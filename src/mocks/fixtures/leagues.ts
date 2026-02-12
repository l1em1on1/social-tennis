import type { League } from "@/models";
import { LeagueType } from "@/models";

import { mockGames } from "./games";

export const mockLeagues: readonly League[] = [
  {
    id: "league-1",
    name: "Spring Singles Men",
    type: LeagueType.SinglesMen,
    playerIds: ["player-1", "player-3", "player-5"],
    stages: [
      {
        id: "stage-1",
        startDate: "2026-01-01T00:00:00.000Z",
        endDate: "2026-03-31T23:59:59.000Z",
        games: [mockGames[0], mockGames[4]],
      },
    ],
  },
  {
    id: "league-2",
    name: "Spring Singles Women",
    type: LeagueType.SinglesWomen,
    playerIds: ["player-2", "player-4", "player-6"],
    stages: [
      {
        id: "stage-2",
        startDate: "2026-01-01T00:00:00.000Z",
        endDate: "2026-03-31T23:59:59.000Z",
        games: [mockGames[1]],
      },
    ],
  },
  {
    id: "league-3",
    name: "Spring Mixed Doubles",
    type: LeagueType.DoublesMixed,
    playerIds: ["player-1", "player-2", "player-3", "player-4", "player-5", "player-6"],
    stages: [
      {
        id: "stage-3",
        startDate: "2026-01-01T00:00:00.000Z",
        endDate: "2026-03-31T23:59:59.000Z",
        games: [mockGames[2], mockGames[3], mockGames[5]],
      },
    ],
  },
];
