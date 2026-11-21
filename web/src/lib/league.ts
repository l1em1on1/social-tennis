import type { components } from "@/lib/api/schema";

type Schemas = components["schemas"];
export type LeagueType = Schemas["LeagueType"];
export type LeagueStructure = Schemas["LeagueStructure"];
export type StageRoll = NonNullable<Schemas["StageRoll"]>;
export type StageLengthUnit = Schemas["StageLengthUnit"];

/** The seven League Types (CONTEXT: League Type), in the order the setup form lists them. */
export const LEAGUE_TYPES: { value: LeagueType; label: string }[] = [
  { value: "SinglesMen", label: "Singles · men" },
  { value: "SinglesWomen", label: "Singles · women" },
  { value: "SinglesMixed", label: "Singles · mixed" },
  { value: "DoublesMen", label: "Doubles · men" },
  { value: "DoublesWomen", label: "Doubles · women" },
  { value: "DoublesMix", label: "Doubles · mixed" },
  { value: "Foursomes", label: "Foursomes" },
];

export const typeLabel = (type: LeagueType) => LEAGUE_TYPES.find((option) => option.value === type)?.label ?? type;

/** Foursomes is a Division-of-4 rotation, meaningless in a Bracket: the API refuses the pair too. */
export const isValidCombination = (type: LeagueType, structure: LeagueStructure) =>
  !(type === "Foursomes" && structure === "Knockout");

/** The generated types allow an integer as text (how the API's schema describes one); it is always a number. */
interface ScoreFormat {
  gamesPerSet: number | string;
  setsPerGame: number | string;
  matchTiebreakDecider: boolean;
}

/** The Score format in words, e.g. "Best of 3 sets of 6, tiebreak at 5–5, match tiebreak decider". */
export function describeScoreFormat({ gamesPerSet, setsPerGame, matchTiebreakDecider }: ScoreFormat): string {
  const games = Number(gamesPerSet);
  const bestOfThree = Number(setsPerGame) === 3;
  return [
    `${bestOfThree ? "Best of 3 sets" : "One set"} of ${games}`,
    `tiebreak at ${games - 1}–${games - 1}`,
    bestOfThree && matchTiebreakDecider ? "match tiebreak decider" : null,
  ]
    .filter(Boolean)
    .join(", ");
}

export function describeStageLength({ count, unit }: { count: number | string; unit: StageLengthUnit }): string {
  const n = Number(count);
  if (unit === "Months") return n === 1 ? "Calendar month" : `${n} months`;
  return n === 1 ? "1 week" : `${n} weeks`;
}

/** "Divisional, closes automatically" / "Knockout, 2 weeks a round". */
export function describeStructure(league: {
  structure: LeagueStructure;
  stageRoll: StageRoll | null;
  stageLength: { count: number | string; unit: StageLengthUnit };
}): string {
  const length = describeStageLength(league.stageLength).toLowerCase();
  if (league.structure === "Knockout") return `Knockout · ${length} a round`;
  return `Divisional · ${length} Stages · ${league.stageRoll === "Automatic" ? "closes automatically" : "Manager closes"}`;
}

/**
 * A League date as the calendar date it is (ADR-0016): "1 Dec 2026". Formatted
 * in UTC from the bare date, so no browser or server zone can move it a day.
 */
export function formatDate(date: string): string {
  return new Date(`${date}T00:00:00Z`).toLocaleDateString("en-GB", {
    day: "numeric",
    month: "short",
    year: "numeric",
    timeZone: "UTC",
  });
}

export const formatDates = (startDate: string, endDate: string) => `${formatDate(startDate)} – ${formatDate(endDate)}`;

/**
 * The Division of the Competitor in `position` (1 at the top) on a ladder of
 * `count` with Divisions of `size`, or null for the Bye: the one left over at
 * the bottom. As the API's `Ladder.DivisionOf` (#7, ADR-0013).
 */
export function divisionOf(position: number, count: number, size = 2): number | null {
  const division = Math.ceil(position / size);
  return division <= Math.floor(count / size) ? division : null;
}
