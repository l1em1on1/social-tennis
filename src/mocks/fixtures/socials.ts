import type { Social } from "@/models";

import { mockGames } from "./games";

export const mockSocials: readonly Social[] = [
  {
    id: "social-1",
    name: "Saturday Social",
    date: "2026-02-15T10:00:00.000Z",
    playerIds: ["player-1", "player-2", "player-3", "player-4"],
    votes: [
      { id: "vote-1", playerId: "player-1", date: "2026-02-15T10:00:00.000Z" },
      { id: "vote-2", playerId: "player-2", date: "2026-02-15T10:00:00.000Z" },
      { id: "vote-3", playerId: "player-3", date: "2026-02-15T14:00:00.000Z" },
    ],
    games: [mockGames[2]],
  },
  {
    id: "social-2",
    name: "Sunday Funday",
    date: "2026-02-22T14:00:00.000Z",
    playerIds: ["player-3", "player-4", "player-5", "player-6"],
    votes: [
      { id: "vote-4", playerId: "player-5", date: "2026-02-22T14:00:00.000Z" },
      { id: "vote-5", playerId: "player-6", date: "2026-02-22T14:00:00.000Z" },
    ],
    games: [mockGames[5]],
  },
];
