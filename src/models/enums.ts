export const Gender = {
  Male: "Male",
  Female: "Female",
} as const;

export type Gender = (typeof Gender)[keyof typeof Gender];

export const LeagueType = {
  SinglesMen: "SinglesMen",
  SinglesWomen: "SinglesWomen",
  SinglesMixed: "SinglesMixed",
  DoublesMen: "DoublesMen",
  DoublesWomen: "DoublesWomen",
  DoublesMixed: "DoublesMixed",
} as const;

export type LeagueType = (typeof LeagueType)[keyof typeof LeagueType];

export const PlayerLevel = {
  Level1: 1,
  Level2: 2,
  Level3: 3,
  Level4: 4,
  Level5: 5,
} as const;

export type PlayerLevel = (typeof PlayerLevel)[keyof typeof PlayerLevel];
