import type { components } from "@/lib/api/schema";

type Schemas = components["schemas"];

/**
 * The mock API's whole world (#234, ADR-0033): fixed data, read by the
 * Chromatic snapshots of every screen (e2e/screens.spec.ts and
 * e2e/manager-pages.spec.ts). Nothing writes to it, so a page looks the same
 * on every run. Behaviour is tested against the real API.
 *
 * Who signs in decides what they see: each persona below is an email, and
 * the mock answers as the real API would for that User.
 */
export const FIXTURE_MANAGER = "snapshot-manager@example.com";
export const FIXTURE_ADMIN = "snapshot-admin@example.com";
/** Alex Morgan, a member, with a child, Sam, on the same login. */
export const FIXTURE_MEMBER = "alex.morgan@example.com";
/** Jo Bennett, a member and a League manager of Riverside (#220): runs Summer Ladder and Autumn Singles. */
export const FIXTURE_LEAGUE_MANAGER = "jo.bennett@example.com";
/** Priya Shah, asking to join Riverside. */
export const FIXTURE_WAITING = "priya.shah@example.com";
/** Signed in for the first time: no Player yet. */
export const FIXTURE_NEWCOMER = "nell.newcomer@example.com";
/** Olly Outside: a Player in no Club. */
export const FIXTURE_OUTSIDER = "olly.outside@example.com";

/** The day it is where the Club is, for every answer that says (ADR-0016). */
export const FIXTURE_TODAY = "2026-11-20";

const id = (kind: string, n: number) => `00000000-0000-4000-8000-f${kind}${String(n).padStart(10, "0")}`;

/** Fixed, so the Admin page marks the fixture Admin's own row as theirs. */
export const FIXTURE_USER_IDS: Record<string, string> = {
  [FIXTURE_MANAGER]: id("a", 1),
  [FIXTURE_ADMIN]: id("a", 2),
  // The Users who hold the Players below.
  "tom.okafor@example.com": id("a", 3),
  "priya.shah@example.com": id("a", 4),
  "chris.lee@example.com": id("a", 5),
  "jo.bennett@example.com": id("a", 6),
  "alex.morgan@example.com": id("a", 7),
  "ella.grant@example.com": id("a", 8),
  "nia.brooks@example.com": id("a", 9),
  [FIXTURE_NEWCOMER]: id("a", 10),
  [FIXTURE_OUTSIDER]: id("a", 11),
};

export const RIVERSIDE = { id: id("c", 1), name: "Riverside Tennis Club" };
export const HILLSIDE = { id: id("c", 2), name: "Hillside Social Tennis" };
export const CLUBS = [RIVERSIDE, HILLSIDE];

/** Riverside's join link is on (#222); Hillside's is off. */
export const FIXTURE_JOIN_CODE = "FixedJoinCodeForSnapsh";

export const FIXTURE_LEAGUE_ID = id("e", 1);
export const FIXTURE_DOUBLES_LEAGUE_ID = id("e", 2);
/** Autumn Singles: started (#7), with a Division of 3 and someone left out; its one Stage, October, closed, so it has ended (#10). */
export const FIXTURE_STARTED_LEAGUE_ID = id("e", 3);
/** Summer Ladder: two Divisions, its Stage under way, Games still to score. Jo runs it. */
export const FIXTURE_RUNNING_LEAGUE_ID = id("e", 4);
/** Spring Ladder: its Stage past its deadline, held up by a tie, with a Team waiting for a place. */
export const FIXTURE_HELD_UP_LEAGUE_ID = id("e", 5);
/** Summer Doubles: not started, with a Team and a Player without a partner. Alex can join it. */
export const FIXTURE_OPEN_DOUBLES_LEAGUE_ID = id("e", 6);

/** Thursday Foursomes (#14): Jo, Nia, Ella and Alex in one Division of 4, two of its 3 Games played. */
export const FIXTURE_FOURSOMES_LEAGUE_ID = id("e", 7);

/** Autumn Singles' Games (#38): Jo beat Alex on a tiebreak, Chris won when Jo stopped early, and Alex v Chris wasn't played (#10). */
export const FIXTURE_GAME_IDS = { played: id("9", 1), retirement: id("9", 2), notPlayed: id("9", 3) } as const;
/** Summer Ladder's Games, by who plays. */
export const FIXTURE_RUNNING_GAME_IDS = {
  joElla: id("9", 4),
  joNia: id("9", 5),
  ellaNia: id("9", 6),
  chrisAlex: id("9", 7),
  chrisSam: id("9", 8),
  alexSam: id("9", 9),
} as const;

/** Thursday Foursomes' Games, by pairs: Jo & Nia v Ella & Alex, Jo & Ella v Nia & Alex, Jo & Alex v Nia & Ella. */
export const FIXTURE_FOURSOMES_GAME_IDS = { joNia: id("9", 20), joElla: id("9", 21), joAlex: id("9", 22) } as const;

const whole = (total: number) => ({ total, offset: 0, limit: null });

const member = (
  n: number,
  name: string,
  email: string,
  rest: Partial<Schemas["ClubMember"]> = {},
): Schemas["ClubMember"] => ({
  playerId: id("b", n),
  userId: FIXTURE_USER_IDS[email],
  name,
  email,
  signedIn: true,
  approved: true,
  gender: null,
  level: null,
  rating: 1,
  requestedAt: "2026-10-01T09:00:00Z",
  ...rest,
});

const alex = member(1, "Alex Morgan", "alex.morgan@example.com", { gender: "Female", level: 3 });
const sam = member(2, "Sam Morgan", "alex.morgan@example.com");
const jo = member(3, "Jo Bennett", "jo.bennett@example.com", { gender: "Male", level: 4 });
const chris = member(4, "Chris Lee", "chris.lee@example.com", { signedIn: false, level: 2 });
const priya = member(5, "Priya Shah", "priya.shah@example.com", { approved: false, requestedAt: "2026-11-12T18:30:00Z" });
const tom = member(6, "Tom Okafor", "tom.okafor@example.com", { approved: false, requestedAt: "2026-11-14T08:05:00Z" });
// Asking to join Spring Doubles together (#6).
const nia = member(7, "Nia Brooks", "nia.brooks@example.com", { gender: "Female", level: 3 });
const ella = member(8, "Ella Grant", "ella.grant@example.com", { gender: "Female", level: 2 });

/** Every Player, by id: the Club's members, and Olly, who is in none. */
export const PLAYERS: Record<string, { id: string; name: string; email: string; gender: Schemas["PlayerGender"] | null; level: number | null }> =
  Object.fromEntries(
    [
      ...[alex, sam, jo, chris, priya, tom, nia, ella].map((m) => ({
        id: m.playerId,
        name: m.name,
        email: m.email!,
        gender: m.gender ?? null,
        level: m.level === null || m.level === undefined ? null : Number(m.level),
      })),
      { id: id("b", 9), name: "Olly Outside", email: FIXTURE_OUTSIDER, gender: null, level: null },
    ].map((player) => [player.id, player]),
  );

/** Riverside has someone in every state; Hillside has no one, so its empty states show. */
export const fixtureMembers: Record<string, Schemas["GetClubMembersResponse"]> = {
  [RIVERSIDE.id]: {
    // The API's order: requests waiting, oldest first, then members by name.
    members: [priya, tom, alex, chris, ella, jo, nia, sam],
    invites: [
      // Addresses with no Player yet: an invite is used up by the first one (#164).
      { id: id("d", 1), email: "dana.price@example.com", invitedAt: "2026-11-10T12:00:00Z" },
      { id: id("d", 2), email: "morgan.ellis@example.com", invitedAt: "2026-11-16T10:30:00Z" },
    ],
    roles: [
      { userId: FIXTURE_USER_IDS[FIXTURE_MANAGER], email: FIXTURE_MANAGER, role: "Manager" },
      // Jo creates leagues, and runs Autumn Singles and Summer Ladder (#220).
      { userId: FIXTURE_USER_IDS[jo.email!], email: jo.email!, role: "LeagueManager" },
    ],
    page: whole(8),
  },
  [HILLSIDE.id]: {
    members: [],
    invites: [],
    roles: [{ userId: FIXTURE_USER_IDS[FIXTURE_MANAGER], email: FIXTURE_MANAGER, role: "Manager" }],
    page: whole(0),
  },
};

/** Each email's Players, with their Clubs, as GET /players lists them. */
export const fixturePlayersOf = (email: string): Schemas["PlayerSummary"][] => {
  const clubs = (playerId: string) =>
    CLUBS.flatMap((club) =>
      (fixtureMembers[club.id]?.members ?? [])
        .filter((m) => m.playerId === playerId)
        .map((m) => ({ id: club.id, name: club.name, approved: m.approved })),
    );
  return Object.values(PLAYERS)
    .filter((player) => player.email === email)
    .map((player) => ({ id: player.id, name: player.name, gender: player.gender, level: player.level, clubs: clubs(player.id) }));
};

const winterSingles = {
  id: FIXTURE_LEAGUE_ID,
  name: "Winter Singles",
  type: "SinglesMixed",
  structure: "Divisional",
  startDate: "2027-01-04",
  endDate: "2027-03-28",
  stageLength: { count: 1, unit: "Months" },
  stageRoll: "Automatic",
  scoreFormat: { gamesPerSet: 6, setsPerGame: 3, matchTiebreakDecider: true },
  divisionSize: 4,
  standingsMode: "Standard",
} as const;

const springDoubles = {
  id: FIXTURE_DOUBLES_LEAGUE_ID,
  name: "Spring Doubles",
  type: "DoublesMix",
  structure: "Knockout",
  startDate: "2027-04-05",
  endDate: "2027-06-27",
  stageLength: { count: 2, unit: "Weeks" },
  stageRoll: null,
  scoreFormat: { gamesPerSet: 6, setsPerGame: 1, matchTiebreakDecider: false },
  divisionSize: null,
  standingsMode: null,
} as const;

const autumnSingles = {
  id: FIXTURE_STARTED_LEAGUE_ID,
  name: "Autumn Singles",
  type: "SinglesMixed",
  structure: "Divisional",
  startDate: "2026-10-01",
  endDate: "2026-10-31",
  stageLength: { count: 1, unit: "Months" },
  stageRoll: "Manual",
  scoreFormat: { gamesPerSet: 6, setsPerGame: 1, matchTiebreakDecider: false },
  divisionSize: 4,
  standingsMode: "Standard",
} as const;

const summerLadder = {
  id: FIXTURE_RUNNING_LEAGUE_ID,
  name: "Summer Ladder",
  type: "SinglesMixed",
  structure: "Divisional",
  startDate: "2026-10-01",
  endDate: "2027-03-31",
  stageLength: { count: 1, unit: "Months" },
  stageRoll: "Automatic",
  scoreFormat: { gamesPerSet: 6, setsPerGame: 1, matchTiebreakDecider: false },
  divisionSize: 3,
  // Ranked by points (#214): see its standings.
  standingsMode: "BonusPoints",
} as const;

const springLadder = {
  id: FIXTURE_HELD_UP_LEAGUE_ID,
  name: "Spring Ladder",
  type: "SinglesMixed",
  structure: "Divisional",
  startDate: "2026-10-01",
  endDate: "2027-01-31",
  stageLength: { count: 1, unit: "Months" },
  stageRoll: "Manual",
  scoreFormat: { gamesPerSet: 6, setsPerGame: 1, matchTiebreakDecider: false },
  divisionSize: 2,
  standingsMode: "Standard",
} as const;

const summerDoubles = {
  id: FIXTURE_OPEN_DOUBLES_LEAGUE_ID,
  name: "Summer Doubles",
  type: "DoublesMix",
  structure: "Divisional",
  startDate: "2027-05-03",
  endDate: "2027-08-29",
  stageLength: { count: 1, unit: "Months" },
  stageRoll: "Automatic",
  scoreFormat: { gamesPerSet: 6, setsPerGame: 3, matchTiebreakDecider: true },
  divisionSize: 4,
  standingsMode: "Standard",
} as const;

const thursdayFoursomes = {
  id: FIXTURE_FOURSOMES_LEAGUE_ID,
  name: "Thursday Foursomes",
  type: "Foursomes",
  structure: "Divisional",
  startDate: "2026-11-01",
  endDate: "2027-02-28",
  stageLength: { count: 1, unit: "Months" },
  stageRoll: "Manual",
  scoreFormat: { gamesPerSet: 9, setsPerGame: 1, matchTiebreakDecider: false },
  divisionSize: null,
  standingsMode: null,
} as const;

const autumnStage = { number: 1, startDate: "2026-10-01", endDate: "2026-10-31" };
const foursomesStage = { number: 1, startDate: "2026-11-01", endDate: "2026-11-30" };
const summerStage = { number: 2, startDate: "2026-11-01", endDate: "2026-11-30" };
const springStage = { number: 1, startDate: "2026-10-01", endDate: "2026-10-31" };

const inLeague = (
  who: Schemas["ClubMember"],
  joinedAt: string,
  teamId: string | null,
): Schemas["LeagueParticipantSummary"] => ({
  playerId: who.playerId,
  name: who.name,
  email: who.email,
  rating: who.rating,
  joinedAt,
  gender: who.gender,
  teamId,
});

/** A Team, with its Rating the average of its Players', as the API works it out, and its place once started (#7). */
const team = (
  n: number,
  members: Schemas["ClubMember"][],
  position: number | null = null,
  division: number | null = null,
): Schemas["LeagueTeamSummary"] => ({
  id: id("f", n),
  playerIds: members.map((m) => m.playerId),
  rating: members.reduce((sum, m) => sum + Number(m.rating), 0) / members.length,
  position,
  division,
});

const asked = (
  who: Schemas["ClubMember"],
  status: Schemas["LeagueJoinRequestStatus"],
  requestedAt: string,
  decidedAt: string | null,
  partner?: Schemas["ClubMember"],
): Schemas["LeagueJoinRequestSummary"] => ({
  playerId: who.playerId,
  name: who.name,
  email: who.email,
  rating: who.rating,
  gender: who.gender,
  partner: partner
    ? { playerId: partner.playerId, name: partner.name, email: partner.email, rating: partner.rating, gender: partner.gender }
    : null,
  status,
  requestedAt,
  decidedAt,
});

const club = { clubId: RIVERSIDE.id, clubName: RIVERSIDE.name };

/**
 * Each League's own page, by id, as its Club's managers see it. Who runs it
 * (`managers`) is listed with emails; the mock blanks them for anyone else.
 */
export const fixtureLeaguePages: Record<string, Schemas["GetLeagueResponse"]> = {
  /**
   * Winter Singles: one request waiting, two in, and two members still to add.
   * Jo's request was turned down and the Manager then added him, which the page
   * allows: it keeps him under Rejected. Requests in the API's order: waiting
   * first, then the latest decided.
   */
  [winterSingles.id]: {
    ...winterSingles,
    ...club,
    participants: [inLeague(alex, "2026-11-02T20:10:00Z", id("f", 1)), inLeague(jo, "2026-11-05T19:20:00Z", id("f", 2))],
    joinRequests: [
      asked(sam, "Pending", "2026-11-14T17:45:00Z", null),
      asked(jo, "Rejected", "2026-11-04T09:00:00Z", "2026-11-05T08:00:00Z"),
      asked(alex, "Approved", "2026-11-02T20:10:00Z", "2026-11-03T10:00:00Z"),
    ],
    teams: [team(1, [alex]), team(2, [jo])],
    stage: null,
    managers: [],
    canAssignManagers: true,
  },
  /**
   * Spring Doubles (mixed): Alex and Jo are a Team, and Sam and Chris, added by
   * the Manager, are still without a partner (#6). Nia asked to join with Ella:
   * both are women, so the page warns but lets the Manager approve.
   */
  [springDoubles.id]: {
    ...springDoubles,
    ...club,
    participants: [
      inLeague(alex, "2026-11-06T10:00:00Z", id("f", 3)),
      inLeague(jo, "2026-11-06T10:05:00Z", id("f", 3)),
      inLeague(sam, "2026-11-07T18:00:00Z", null),
      inLeague(chris, "2026-11-08T09:30:00Z", null),
    ],
    joinRequests: [asked(nia, "Pending", "2026-11-15T19:00:00Z", null, ella)],
    teams: [team(3, [alex, jo])],
    stage: null,
    managers: [],
    canAssignManagers: true,
  },
  /**
   * Autumn Singles, started (#7): Jo, Alex and Chris are Division 1, each
   * playing the other two (ADR-0031), and the Manager left Sam out, so he is in
   * the League without a place.
   */
  [autumnSingles.id]: {
    ...autumnSingles,
    ...club,
    participants: [
      inLeague(alex, "2026-09-10T18:00:00Z", id("f", 5)),
      inLeague(jo, "2026-09-11T09:30:00Z", id("f", 4)),
      inLeague(chris, "2026-09-12T12:00:00Z", id("f", 6)),
      inLeague(sam, "2026-09-20T20:15:00Z", id("f", 7)),
    ],
    joinRequests: [],
    teams: [team(5, [alex], 2, 1), team(4, [jo], 1, 1), team(6, [chris], 3, 1), team(7, [sam])],
    stage: autumnStage,
    managers: [{ userId: FIXTURE_USER_IDS[jo.email!], name: jo.name, email: jo.email }],
    canAssignManagers: true,
  },
  /** Summer Ladder: Jo, Nia and Ella in Division 1; Chris, Sam and Alex in Division 2. Jo runs it. */
  [summerLadder.id]: {
    ...summerLadder,
    ...club,
    participants: [
      inLeague(jo, "2026-09-02T18:00:00Z", id("f", 10)),
      inLeague(ella, "2026-09-03T18:00:00Z", id("f", 11)),
      inLeague(nia, "2026-09-04T18:00:00Z", id("f", 12)),
      inLeague(chris, "2026-09-05T18:00:00Z", id("f", 13)),
      inLeague(alex, "2026-09-06T18:00:00Z", id("f", 14)),
      inLeague(sam, "2026-09-07T18:00:00Z", id("f", 15)),
    ],
    joinRequests: [],
    teams: [
      team(10, [jo], 1, 1),
      team(12, [nia], 2, 1),
      team(11, [ella], 3, 1),
      team(13, [chris], 4, 2),
      team(15, [sam], 5, 2),
      team(14, [alex], 6, 2),
    ],
    stage: summerStage,
    managers: [{ userId: FIXTURE_USER_IDS[jo.email!], name: jo.name, email: jo.email }],
    canAssignManagers: true,
  },
  /** Spring Ladder: Jo and Chris in Division 1, Ella and Nia in Division 2, and Sam waiting for a place. */
  [springLadder.id]: {
    ...springLadder,
    ...club,
    participants: [
      inLeague(jo, "2026-09-02T18:00:00Z", id("f", 20)),
      inLeague(chris, "2026-09-05T18:00:00Z", id("f", 21)),
      inLeague(ella, "2026-09-03T18:00:00Z", id("f", 22)),
      inLeague(nia, "2026-09-04T18:00:00Z", id("f", 23)),
      inLeague(sam, "2026-10-12T19:00:00Z", id("f", 24)),
    ],
    joinRequests: [],
    teams: [team(20, [jo], 1, 1), team(21, [chris], 2, 1), team(22, [ella], 3, 2), team(23, [nia], 4, 2), team(24, [sam])],
    stage: springStage,
    managers: [],
    canAssignManagers: true,
  },
  /** Thursday Foursomes: Jo, Nia, Ella and Alex, each a Team of one (ADR-0036), in one Division. */
  [thursdayFoursomes.id]: {
    ...thursdayFoursomes,
    ...club,
    participants: [
      inLeague(jo, "2026-10-20T18:00:00Z", id("f", 40)),
      inLeague(nia, "2026-10-21T18:00:00Z", id("f", 41)),
      inLeague(ella, "2026-10-22T18:00:00Z", id("f", 42)),
      inLeague(alex, "2026-10-23T18:00:00Z", id("f", 43)),
    ],
    joinRequests: [],
    teams: [team(40, [jo], 1, 1), team(41, [nia], 2, 1), team(42, [ella], 3, 1), team(43, [alex], 4, 1)],
    stage: foursomesStage,
    managers: [],
    canAssignManagers: true,
  },
  /** Summer Doubles: Jo and Nia are a Team, and Ella waits for a partner. */
  [summerDoubles.id]: {
    ...summerDoubles,
    ...club,
    participants: [
      inLeague(jo, "2026-11-10T19:00:00Z", id("f", 30)),
      inLeague(nia, "2026-11-10T19:05:00Z", id("f", 30)),
      inLeague(ella, "2026-11-11T08:30:00Z", null),
    ],
    joinRequests: [],
    teams: [team(30, [jo, nia])],
    stage: null,
    managers: [],
    canAssignManagers: true,
  },
};

/** A Club's Leagues, newest first as the API lists them. */
export const LEAGUES_BY_START = Object.values(fixtureLeaguePages).sort(
  (a, b) => b.startDate.localeCompare(a.startDate) || a.name.localeCompare(b.name),
);

/** Who runs each League, besides its Club's managers (#220): by email. */
export const LEAGUE_RUNNERS: Record<string, string[]> = {
  [autumnSingles.id]: [jo.email!],
  [summerLadder.id]: [jo.email!],
};

const score = (winner: 1 | 2, first: number, second: number, extra: Partial<Schemas["SetScore"]> = {}): Schemas["LadderGameScore"] => ({
  winner,
  retired: false,
  sets: [{ first, second, firstTiebreak: null, secondTiebreak: null, matchTiebreak: false, ...extra }],
});

const autumnScores = {
  played: score(1, 6, 5, { firstTiebreak: 7, secondTiebreak: 4 }),
  retirement: { ...score(2, 2, 4), retired: true },
} satisfies Record<string, Schemas["LadderGameScore"]>;

/** Jo v Alex as Alex first entered it, the tiebreak points the wrong way round. */
const alexFirst = score(2, 5, 6, { firstTiebreak: 4, secondTiebreak: 7 });

const competitor = (n: number, position: number, who: Schemas["ClubMember"]): Schemas["LadderCompetitor"] => ({
  teamId: id("f", n),
  position,
  players: [{ playerId: who.playerId, name: who.name }],
});

const standing = (
  n: number,
  rank: number,
  [played, won, lost, gamesFor, gamesAgainst]: number[],
  move: Schemas["StandingMove"] | null,
  tied = false,
  points?: number,
): Schemas["LadderStanding"] => ({ teamId: id("f", n), rank, played, won, lost, gamesFor, gamesAgainst, tied, move, points });

const G = FIXTURE_RUNNING_GAME_IDS;
const F = FIXTURE_FOURSOMES_GAME_IDS;

/** Each started League's ladder, as its Club's managers see it; the mock sets `yourPlayerIds` and `canManage` per User. */
export const fixtureLadders: Record<string, Schemas["GetLeagueLadderResponse"]> = {
  [autumnSingles.id]: {
    id: autumnSingles.id,
    name: autumnSingles.name,
    clubName: RIVERSIDE.name,
    type: autumnSingles.type,
    stage: autumnStage,
    yourPlayerIds: [],
    // October, its one Stage, is closed (see its Games): the League has ended (#10).
    heldUp: false,
    awaiting: { unscoredGameIds: [], ties: [] },
    ended: true,
    managers: [{ name: jo.name }],
    canManage: true,
    divisions: [
      {
        number: 1,
        playersTies: [],
        competitors: [competitor(4, 1, jo), competitor(5, 2, alex), competitor(6, 3, chris)],
        // Every pair once, the higher place first (ADR-0031).
        games: [
          { id: FIXTURE_GAME_IDS.played, firstTeamId: id("f", 4), secondTeamId: id("f", 5), score: autumnScores.played, notPlayed: false },
          { id: FIXTURE_GAME_IDS.retirement, firstTeamId: id("f", 4), secondTeamId: id("f", 6), score: autumnScores.retirement, notPlayed: false },
          // Not played (#10): Alex and Chris never found a date.
          { id: FIXTURE_GAME_IDS.notPlayed, firstTeamId: id("f", 5), secondTeamId: id("f", 6), score: null, notPlayed: true },
        ],
        // Chris 4–2 and Jo 8–9 both won once, so games difference puts Chris first (#10).
        standings: [standing(6, 1, [1, 1, 0, 4, 2], "Stays"), standing(4, 2, [2, 1, 1, 8, 9], "Stays"), standing(5, 3, [1, 0, 1, 5, 6], "Stays")],
      },
    ],
  },
  [summerLadder.id]: {
    id: summerLadder.id,
    name: summerLadder.name,
    clubName: RIVERSIDE.name,
    type: summerLadder.type,
    stage: summerStage,
    yourPlayerIds: [],
    heldUp: false,
    awaiting: { unscoredGameIds: [G.ellaNia, G.chrisAlex], ties: [] },
    ended: false,
    managers: [{ name: jo.name }],
    canManage: true,
    divisions: [
      {
        number: 1,
        playersTies: [],
        competitors: [competitor(10, 1, jo), competitor(12, 2, nia), competitor(11, 3, ella)],
        games: [
          { id: G.joElla, firstTeamId: id("f", 10), secondTeamId: id("f", 11), score: score(1, 6, 3), notPlayed: false },
          { id: G.joNia, firstTeamId: id("f", 10), secondTeamId: id("f", 12), score: score(1, 6, 4), notPlayed: false },
          { id: G.ellaNia, firstTeamId: id("f", 12), secondTeamId: id("f", 11), score: null, notPlayed: false },
        ],
        // Bonus points (#214): Jo 2 won + 2 played + 1 for playing both; Nia and Ella 1 each, games difference between them.
        standings: [
          standing(10, 1, [2, 2, 0, 12, 7], "Stays", false, 5),
          standing(12, 2, [1, 0, 1, 4, 6], "Stays", false, 1),
          standing(11, 3, [1, 0, 1, 3, 6], "Down", false, 1),
        ],
      },
      {
        number: 2,
        playersTies: [],
        competitors: [competitor(13, 4, chris), competitor(15, 5, sam), competitor(14, 6, alex)],
        games: [
          { id: G.chrisSam, firstTeamId: id("f", 13), secondTeamId: id("f", 15), score: score(1, 6, 2), notPlayed: false },
          // Arranged (#9): Chris set it for yesterday, and nobody has entered the score yet.
          { id: G.chrisAlex, firstTeamId: id("f", 13), secondTeamId: id("f", 14), score: null, notPlayed: false, date: "2026-11-19", court: "6 - astro" },
          { id: G.alexSam, firstTeamId: id("f", 15), secondTeamId: id("f", 14), score: score(1, 6, 4), notPlayed: false },
        ],
        // Sam and Chris won once each, but Sam has played both Games: 1 + 2 + 1 beats Chris's 1 + 1.
        standings: [
          standing(15, 1, [2, 1, 1, 8, 10], "Up", false, 4),
          standing(13, 2, [1, 1, 0, 6, 2], "Stays", false, 2),
          standing(14, 3, [1, 0, 1, 4, 6], "Stays", false, 1),
        ],
      },
    ],
  },
  [springLadder.id]: {
    id: springLadder.id,
    name: springLadder.name,
    clubName: RIVERSIDE.name,
    type: springLadder.type,
    stage: springStage,
    yourPlayerIds: [],
    // October's deadline has passed, and Ella and Nia never played: who goes up is the Manager's to decide.
    heldUp: true,
    awaiting: { unscoredGameIds: [], ties: [{ division: 2, teamIds: [id("f", 22), id("f", 23)] }] },
    ended: false,
    managers: [],
    canManage: true,
    divisions: [
      {
        number: 1,
        playersTies: [],
        competitors: [competitor(20, 1, jo), competitor(21, 2, chris)],
        games: [{ id: id("9", 10), firstTeamId: id("f", 20), secondTeamId: id("f", 21), score: score(1, 6, 4), notPlayed: false }],
        standings: [standing(20, 1, [1, 1, 0, 6, 4], "Stays"), standing(21, 2, [1, 0, 1, 4, 6], "Down")],
      },
      {
        number: 2,
        playersTies: [],
        competitors: [competitor(22, 3, ella), competitor(23, 4, nia)],
        games: [{ id: id("9", 11), firstTeamId: id("f", 22), secondTeamId: id("f", 23), score: null, notPlayed: true }],
        standings: [standing(22, 1, [0, 0, 0, 0, 0], null, true), standing(23, 1, [0, 0, 0, 0, 0], null, true)],
      },
    ],
  },
  /** Thursday Foursomes' ladder (#14): 3 Games of rotating pairs, ranked by games banked; one Division, so nobody moves. */
  [thursdayFoursomes.id]: {
    id: thursdayFoursomes.id,
    name: thursdayFoursomes.name,
    clubName: RIVERSIDE.name,
    type: thursdayFoursomes.type,
    stage: foursomesStage,
    yourPlayerIds: [],
    heldUp: false,
    awaiting: { unscoredGameIds: [F.joAlex], ties: [] },
    ended: false,
    managers: [],
    canManage: true,
    divisions: [
      {
        number: 1,
        playersTies: [],
        competitors: [competitor(40, 1, jo), competitor(41, 2, nia), competitor(42, 3, ella), competitor(43, 4, alex)],
        games: [
          { id: F.joNia, firstTeamId: id("f", 40), firstPartnerTeamId: id("f", 41), secondTeamId: id("f", 42), secondPartnerTeamId: id("f", 43), score: score(1, 9, 6), notPlayed: false },
          { id: F.joElla, firstTeamId: id("f", 40), firstPartnerTeamId: id("f", 42), secondTeamId: id("f", 41), secondPartnerTeamId: id("f", 43), score: score(2, 5, 9), notPlayed: false },
          { id: F.joAlex, firstTeamId: id("f", 40), firstPartnerTeamId: id("f", 43), secondTeamId: id("f", 41), secondPartnerTeamId: id("f", 42), score: null, notPlayed: false },
        ],
        // Games banked: Nia 9 + 9 = 18, Alex 6 + 9 = 15, Jo 9 + 5 = 14, Ella 6 + 5 = 11.
        standings: [
          standing(41, 1, [2, 2, 0, 18, 11], "Stays"),
          standing(43, 2, [2, 1, 1, 15, 14], "Stays"),
          standing(40, 3, [2, 1, 1, 14, 15], "Stays"),
          standing(42, 4, [2, 0, 2, 11, 18], "Stays"),
        ],
      },
    ],
  },
};

/** Closing Spring Ladder's Stage 1 (#10): the tie to order, Sam waiting, and November as Stage 2. */
export const fixtureCloses: Record<string, Schemas["GetStageCloseResponse"]> = {
  [springLadder.id]: {
    stage: springStage,
    leagueEndDate: springLadder.endDate,
    heldUp: true,
    divisions: fixtureLadders[springLadder.id].divisions,
    awaiting: fixtureLadders[springLadder.id].awaiting,
    waiting: [{ teamId: id("f", 24), players: [{ playerId: sam.playerId, name: sam.name }] }],
    nextStage: { startDate: "2026-11-01", endDate: "2026-11-30" },
  },
  [summerLadder.id]: {
    stage: summerStage,
    leagueEndDate: summerLadder.endDate,
    heldUp: false,
    divisions: fixtureLadders[summerLadder.id].divisions,
    awaiting: fixtureLadders[summerLadder.id].awaiting,
    waiting: [],
    nextStage: { startDate: "2026-12-01", endDate: "2026-12-31" },
  },
};

/** Starting Winter Singles (#7): Alex and Jo, by join time, as every Rating is 1; Sam's request is still waiting. */
export const fixtureLeagueStarts: Record<string, Schemas["GetLeagueStartResponse"]> = {
  [winterSingles.id]: {
    competitors: [
      { teamId: id("f", 1), players: [{ playerId: alex.playerId, name: alex.name }], rating: 1, joinedAt: "2026-11-02T20:10:00Z" },
      { teamId: id("f", 2), players: [{ playerId: jo.playerId, name: jo.name }], rating: 1, joinedAt: "2026-11-05T19:20:00Z" },
    ],
    divisionSize: 4,
    divisions: [2],
    unpaired: [],
    waitingRequestCount: 1,
    firstStage: { number: 1, startDate: "2027-01-04", endDate: "2027-02-03" },
  },
};

const user = (
  email: string,
  players: { id: string; name: string }[],
  roles: Partial<Schemas["UserSummary"]> = {},
): Schemas["UserSummary"] => ({
  id: FIXTURE_USER_IDS[email],
  email,
  clubs: [],
  admin: false,
  adminBySetting: false,
  lastAdmin: false,
  players,
  ...roles,
});

const holds = (email: string) =>
  Object.values(PLAYERS)
    .filter((player) => player.email === email)
    .map(({ id, name }) => ({ id, name }));

/**
 * Newest first, as the API lists them: everyone who holds a Player above
 * (Chris Lee's User was made when a Manager added him), then the personas.
 */
export const fixtureUsers: Schemas["UserSummary"][] = [
  user(FIXTURE_OUTSIDER, holds(FIXTURE_OUTSIDER)),
  user(FIXTURE_NEWCOMER, []),
  user(ella.email!, holds(ella.email!)),
  user(nia.email!, holds(nia.email!)),
  user(tom.email!, holds(tom.email!)),
  user(priya.email!, holds(priya.email!)),
  user(chris.email!, holds(chris.email!)),
  user(jo.email!, holds(jo.email!), { clubs: [{ clubId: RIVERSIDE.id, clubName: RIVERSIDE.name, role: "LeagueManager" }] }),
  user(alex.email!, holds(alex.email!)),
  user(FIXTURE_MANAGER, [], { clubs: CLUBS.map(({ id, name }) => ({ clubId: id, clubName: name, role: "Manager" as const })) }),
  user(FIXTURE_ADMIN, [], { admin: true, adminBySetting: true }),
];

const snapshot = (gameScore: Schemas["LadderGameScore"], playedOn: string) => ({ ...gameScore, playedOn });

/** Who set each arranged Game's Date (#9), by Game id. */
export const fixtureDateSetBy: Record<string, string> = { [G.chrisAlex]: chris.name };

/**
 * The days that suit everyone in a Game (#9), as its own Players see them,
 * by Game id: Chris and Alex share two, and Ella hasn't said which days she's free.
 */
export const fixtureGameAvailability: Record<string, Schemas["GameAvailability"]> = {
  [G.chrisAlex]: { sharedDates: ["2026-11-24", "2026-11-27"], notGiven: [] },
  [G.ellaNia]: { sharedDates: [], notGiven: [ella.name] },
};

/** Each Player's free days in a League's current Stage (#9), by League and Player: Alex's in Summer Ladder. */
export const fixtureFreeDays: Record<string, Record<string, string[]>> = {
  [summerLadder.id]: { [alex.playerId]: ["2026-11-21", "2026-11-24", "2026-11-27"] },
};

/** A Game's score and history, by id: those with a score, and how it came to be. */
export const fixtureGameScores: Record<string, Pick<Schemas["GetGameResponse"], "score" | "history">> = {
  // Alex entered it as Alex's win; Jo corrected the tiebreak the next morning.
  [FIXTURE_GAME_IDS.played]: {
    score: { ...autumnScores.played, playedOn: "2026-10-08", submittedBy: alex.name, recordedByManager: false, submittedAt: "2026-10-08T19:40:00Z", version: 812 },
    history: [
      { kind: "Corrected", by: jo.name, at: "2026-10-09T07:15:00Z", before: snapshot(alexFirst, "2026-10-08"), after: snapshot(autumnScores.played, "2026-10-08") },
      { kind: "Entered", by: alex.name, at: "2026-10-08T19:40:00Z", before: null, after: snapshot(alexFirst, "2026-10-08") },
    ],
  },
  [FIXTURE_GAME_IDS.retirement]: {
    score: { ...autumnScores.retirement, playedOn: "2026-10-12", submittedBy: jo.name, recordedByManager: false, submittedAt: "2026-10-12T18:05:00Z", version: 640 },
    history: [{ kind: "Entered", by: jo.name, at: "2026-10-12T18:05:00Z", before: null, after: snapshot(autumnScores.retirement, "2026-10-12") }],
  },
  [G.joElla]: entered(score(1, 6, 3), "2026-11-04", jo.name, "2026-11-04T20:10:00Z"),
  [G.joNia]: entered(score(1, 6, 4), "2026-11-09", nia.name, "2026-11-09T21:00:00Z"),
  [G.chrisSam]: entered(score(1, 6, 2), "2026-11-06", chris.name, "2026-11-06T18:30:00Z"),
  [G.alexSam]: entered(score(1, 6, 4), "2026-11-15", sam.name, "2026-11-15T12:20:00Z"),
  [id("9", 10)]: entered(score(1, 6, 4), "2026-10-20", jo.name, "2026-10-20T19:00:00Z"),
  [F.joNia]: entered(score(1, 9, 6), "2026-11-05", ella.name, "2026-11-05T21:30:00Z"),
  [F.joElla]: entered(score(2, 5, 9), "2026-11-12", alex.name, "2026-11-12T21:15:00Z"),
};

function entered(gameScore: Schemas["LadderGameScore"], playedOn: string, by: string, at: string): Pick<Schemas["GetGameResponse"], "score" | "history"> {
  return {
    score: { ...gameScore, playedOn, submittedBy: by, recordedByManager: false, submittedAt: at, version: 100 },
    history: [{ kind: "Entered", by, at, before: null, after: snapshot(gameScore, playedOn) }],
  };
}

/** Whether a League's current Stage has closed: Autumn's October, its last, has; the others' haven't. A latest Stage closes only as the League's last (#10). */
export const STAGE_CLOSED: Record<string, boolean> = { [autumnSingles.id]: true };

/** The Club's time zone, for the history's times (ADR-0016). */
export const TIME_ZONE = "Europe/London";
