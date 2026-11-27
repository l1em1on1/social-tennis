import type { components } from "@/lib/api/schema";

type Schemas = components["schemas"];
export type LeagueType = Schemas["LeagueType"];
export type LeagueStructure = Schemas["LeagueStructure"];
export type StageRoll = NonNullable<Schemas["StageRoll"]>;
export type StageLengthUnit = Schemas["StageLengthUnit"];
export type StandingsMode = NonNullable<Schemas["StandingsMode"]>;

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

/**
 * Who runs a League, to its Players (#220): "Run by Ada and Bo", or "Run by the
 * club's managers" when it has no manager of its own. A manager with no Player
 * in the Club is "a manager": names, never emails.
 */
export function describeManagers(managers: { name: string | null }[]): string {
  if (managers.length === 0) return "Run by the club's managers";
  const names = managers.map((manager) => manager.name ?? "a manager");
  // Two without a Player read the same: say how many rather than repeat it.
  const unnamed = names.filter((name) => name === "a manager").length;
  const listed = unnamed > 1 ? [...names.filter((name) => name !== "a manager"), `${unnamed} managers`] : names;
  return `Run by ${new Intl.ListFormat("en-GB").format(listed)}`;
}

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

/** "Divisional · … · closes automatically" / "Knockout · 2 weeks a round". Bonus points are said; Standard isn't (#214). */
export function describeStructure(league: {
  structure: LeagueStructure;
  stageRoll: StageRoll | null;
  stageLength: { count: number | string; unit: StageLengthUnit };
  divisionSize?: number | string | null;
  standingsMode?: StandingsMode | null;
}): string {
  const length = describeStageLength(league.stageLength).toLowerCase();
  if (league.structure === "Knockout") return `Knockout · ${length} a round`;
  const divisions = league.divisionSize ? ` · Divisions of ${league.divisionSize}` : "";
  const points = league.standingsMode === "BonusPoints" ? " · Bonus points" : "";
  return `Divisional${divisions}${points} · ${length} Stages · ${league.stageRoll === "Automatic" ? "closes automatically" : "Manager closes"}`;
}

/**
 * How a Division's Standings rank (#214, ADR-0035), for the setup form and
 * under the ladder. Foursomes has its own rule, games won, and no choice.
 */
export const STANDINGS_MODES: { value: StandingsMode; label: string; rule: string }[] = [
  { value: "Standard", label: "Standard", rule: "Ranked by wins, then games difference, then the results between them." },
  {
    value: "BonusPoints",
    label: "Bonus points",
    rule: "A point a win, a point a game played and one more for playing them all; level on points, then games difference, then the results between them.",
  },
];

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

/** A Game's Sides, as Team ids: one Team each, or in Foursomes a pair of Players' Teams of one (#14, ADR-0036). */
export function sidesOf(game: {
  firstTeamId: string;
  secondTeamId: string;
  firstPartnerTeamId?: string | null;
  secondPartnerTeamId?: string | null;
}): [string[], string[]] {
  return [
    [game.firstTeamId, ...(game.firstPartnerTeamId ? [game.firstPartnerTeamId] : [])],
    [game.secondTeamId, ...(game.secondPartnerTeamId ? [game.secondPartnerTeamId] : [])],
  ];
}

/**
 * The Side `gameLabel` names first: Side 1, unless the Game is Foursomes and
 * `mine` is on Side 2. Its score is then shown from Side 2's side too
 * (`formatSets`), so the games read against the right pair.
 */
export function labelledSide(game: Parameters<typeof sidesOf>[0], mine: string[] = []): 1 | 2 {
  const [first, second] = sidesOf(game);
  const ours = (side: string[]) => side.some((team) => mine.includes(team));
  return first.length > 1 && !ours(first) && ours(second) ? 2 : 1;
}

/**
 * A Game in words: "Tom Reid v Omar Ali", or in Foursomes "Ann & Bob v Cat &
 * Dan". With `mine`, a Foursomes Player's own pair comes first, them first,
 * so the partner is named first after them (wireframe P5): "Dan & Ann v Bob &
 * Cat"; show its score from `labelledSide`. Team Games keep the ladder's order.
 */
export function gameLabel(game: Parameters<typeof sidesOf>[0], name: (teamId: string) => string, mine: string[] = []): string {
  const [first, second] = sidesOf(game);
  const ordered = labelledSide(game, mine) === 2 ? [second, first] : [first, second];
  const mineFirst = (side: string[]) => [...side].sort((a, b) => Number(mine.includes(b)) - Number(mine.includes(a)));
  return ordered.map((side) => mineFirst(side).map(name).join(" & ")).join(" v ");
}

/** Teams per Division, as the API's League (ADR-0031): 4 unless the Manager says, from 2 to 8. */
export const DIVISION_SIZE = { default: 4, min: 2, max: 8 } as const;

/** A Division's Games in a Stage: every pair once. */
export const gamesIn = (size: number) => (size * (size - 1)) / 2;

/**
 * As the API's Relegation.Next (#10, ADR-0031): the next Stage's Divisions,
 * top first, from this Stage's in their final order. Each Division's first
 * goes up to the bottom of the one above, its last down to the top of the
 * one below; the ends of the ladder stay put, and `joining` Teams start at
 * the bottom of the bottom Division. The close screen previews it.
 */
export function relegate(divisions: string[][], joining: string[]): string[][] {
  const count = divisions.length;
  return divisions.map((division, index) => {
    const up = index > 0 ? division[0] : null;
    const down = index < count - 1 ? division[division.length - 1] : null;
    return [
      ...(index > 0 ? [divisions[index - 1][divisions[index - 1].length - 1]] : []),
      ...division.filter((team) => team !== up && team !== down),
      ...(index < count - 1 ? [divisions[index + 1][0]] : []),
      ...(index === count - 1 ? joining : []),
    ];
  });
}
