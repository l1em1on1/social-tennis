import type { components } from "@/lib/api/schema";

type Schemas = components["schemas"];

/**
 * A fixed world for the Chromatic snapshots of the Manager and Admin pages
 * (#187). The rest of the mock is shared by the whole suite, which runs in
 * parallel, so those pages list whatever other tests have added by then, with
 * the dates they ran. A session signed in with one of these addresses reads
 * this instead, and so does any request for one of its ids. It only looks:
 * nothing writes to it.
 */
export const FIXTURE_MANAGER = "snapshot-manager@example.com";
export const FIXTURE_ADMIN = "snapshot-admin@example.com";

const id = (kind: string, n: number) => `00000000-0000-4000-8000-f${kind}${String(n).padStart(10, "0")}`;

/** Fixed, so the Admin page marks the fixture Admin's own row as theirs. */
export const FIXTURE_USER_IDS: Record<string, string> = {
  [FIXTURE_MANAGER]: id("a", 1),
  [FIXTURE_ADMIN]: id("a", 2),
};

const RIVERSIDE = { id: id("c", 1), name: "Riverside Tennis Club" };
const HILLSIDE = { id: id("c", 2), name: "Hillside Social Tennis" };
export const FIXTURE_LEAGUE_ID = id("e", 1);
export const FIXTURE_DOUBLES_LEAGUE_ID = id("e", 2);
/** Autumn Singles: started (#7), with a Division of 3 and someone left out. */
export const FIXTURE_STARTED_LEAGUE_ID = id("e", 3);
/** Its Games (#38): Jo beat Alex on a tiebreak, Chris won when Jo stopped early, and Alex v Chris wasn't played (#10). */
export const FIXTURE_GAME_IDS = { played: id("9", 1), retirement: id("9", 2), notPlayed: id("9", 3) } as const;

const whole = (total: number) => ({ total, offset: 0, limit: null });

/** By name, as the API lists them. */
export const fixtureClubs: Schemas["GetClubsResponse"] = { clubs: [HILLSIDE, RIVERSIDE], page: whole(2) };

const member = (
  n: number,
  name: string,
  email: string,
  rest: Partial<Schemas["ClubMember"]> = {},
): Schemas["ClubMember"] => ({
  playerId: id("b", n),
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
    page: whole(8),
  },
  [HILLSIDE.id]: { members: [], invites: [], page: whole(0) },
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
} as const;

const autumnSingles = {
  id: FIXTURE_STARTED_LEAGUE_ID,
  name: "Autumn Singles",
  type: "SinglesMixed",
  structure: "Divisional",
  startDate: "2026-10-01",
  endDate: "2026-12-31",
  stageLength: { count: 1, unit: "Months" },
  stageRoll: "Manual",
  scoreFormat: { gamesPerSet: 6, setsPerGame: 1, matchTiebreakDecider: false },
  divisionSize: 4,
} as const;

const autumnStage = { number: 1, startDate: "2026-10-01", endDate: "2026-10-31" };

export const fixtureLeagues: Record<string, Schemas["GetClubLeaguesResponse"]> = {
  [RIVERSIDE.id]: {
    leagues: [
      { ...springDoubles, participantCount: 4, pendingRequestCount: 1, stage: null },
      { ...winterSingles, participantCount: 2, pendingRequestCount: 1, stage: null },
      { ...autumnSingles, participantCount: 4, pendingRequestCount: 0, stage: autumnStage },
    ],
    page: whole(3),
  },
  [HILLSIDE.id]: { leagues: [], page: whole(0) },
};

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
  playerIds: members.map((member) => member.playerId),
  rating: members.reduce((sum, member) => sum + Number(member.rating), 0) / members.length,
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

/**
 * Winter Singles: one request waiting, two in, and two members still to add.
 * Jo's request was turned down and the Manager then added him, which the page
 * allows: it keeps him under Rejected. Only signed-in Players ask, so Chris,
 * who hasn't signed in, has no request. Requests in the API's order: waiting
 * first, then the latest decided.
 */
const winterSinglesPage: Schemas["GetLeagueResponse"] = {
  ...winterSingles,
  clubId: RIVERSIDE.id,
  clubName: RIVERSIDE.name,
  // An approved request joins at the time it was asked; a Player the Manager adds, at the add.
  // In singles, each is a Team of one (#6).
  participants: [inLeague(alex, "2026-11-02T20:10:00Z", id("f", 1)), inLeague(jo, "2026-11-05T19:20:00Z", id("f", 2))],
  joinRequests: [
    asked(sam, "Pending", "2026-11-14T17:45:00Z", null),
    asked(jo, "Rejected", "2026-11-04T09:00:00Z", "2026-11-05T08:00:00Z"),
    asked(alex, "Approved", "2026-11-02T20:10:00Z", "2026-11-03T10:00:00Z"),
  ],
  teams: [team(1, [alex]), team(2, [jo])],
  stage: null,
};

/**
 * Spring Doubles (mixed): Alex and Jo are a Team, and Sam and Chris, added by
 * the Manager, are still without a partner (#6). Nia asked to join with Ella:
 * both are women, so the page warns but lets the Manager approve.
 */
const springDoublesPage: Schemas["GetLeagueResponse"] = {
  ...springDoubles,
  clubId: RIVERSIDE.id,
  clubName: RIVERSIDE.name,
  participants: [
    inLeague(alex, "2026-11-06T10:00:00Z", id("f", 3)),
    inLeague(jo, "2026-11-06T10:05:00Z", id("f", 3)),
    inLeague(sam, "2026-11-07T18:00:00Z", null),
    inLeague(chris, "2026-11-08T09:30:00Z", null),
  ],
  joinRequests: [asked(nia, "Pending", "2026-11-15T19:00:00Z", null, ella)],
  teams: [team(3, [alex, jo])],
  stage: null,
};

/**
 * Autumn Singles, started (#7): Jo, Alex and Chris are Division 1, each
 * playing the other two (ADR-0031), and the Manager left Sam out, so he is in
 * the League without a place.
 */
const autumnSinglesPage: Schemas["GetLeagueResponse"] = {
  ...autumnSingles,
  clubId: RIVERSIDE.id,
  clubName: RIVERSIDE.name,
  participants: [
    inLeague(alex, "2026-09-10T18:00:00Z", id("f", 5)),
    inLeague(jo, "2026-09-11T09:30:00Z", id("f", 4)),
    inLeague(chris, "2026-09-12T12:00:00Z", id("f", 6)),
    inLeague(sam, "2026-09-20T20:15:00Z", id("f", 7)),
  ],
  joinRequests: [],
  teams: [team(5, [alex], 2, 1), team(4, [jo], 1, 1), team(6, [chris], 3, 1), team(7, [sam])],
  stage: autumnStage,
};

/** Autumn Singles' ladder (#7), as its Players and Managers see it. */
const autumnScores = {
  played: { winner: 1, retired: false, sets: [{ first: 6, second: 5, firstTiebreak: 7, secondTiebreak: 4, matchTiebreak: false }] },
  retirement: { winner: 2, retired: true, sets: [{ first: 2, second: 4, firstTiebreak: null, secondTiebreak: null, matchTiebreak: false }] },
} satisfies Record<string, Schemas["LadderGameScore"]>;

/** Jo v Alex as Alex first entered it, the tiebreak points the wrong way round. */
const alexFirst: Schemas["LadderGameScore"] = {
  winner: 2,
  retired: false,
  sets: [{ first: 5, second: 6, firstTiebreak: 4, secondTiebreak: 7, matchTiebreak: false }],
};

export const fixtureLadders: Record<string, Schemas["GetLeagueLadderResponse"]> = {
  [autumnSingles.id]: {
    id: autumnSingles.id,
    name: autumnSingles.name,
    clubName: RIVERSIDE.name,
    type: autumnSingles.type,
    stage: autumnStage,
    // The fixture Manager holds no Player.
    yourPlayerIds: [],
    // October is closed (see its Games): nothing waits on the Manager.
    heldUp: false,
    awaiting: { unscoredGameIds: [], ties: [] },
    ended: false,
    divisions: [
      {
        number: 1,
        competitors: [
          { teamId: id("f", 4), position: 1, players: [{ playerId: jo.playerId, name: jo.name }] },
          { teamId: id("f", 5), position: 2, players: [{ playerId: alex.playerId, name: alex.name }] },
          { teamId: id("f", 6), position: 3, players: [{ playerId: chris.playerId, name: chris.name }] },
        ],
        // Every pair once, the higher place first (ADR-0031).
        games: [
          { id: FIXTURE_GAME_IDS.played, firstTeamId: id("f", 4), secondTeamId: id("f", 5), score: autumnScores.played, notPlayed: false },
          { id: FIXTURE_GAME_IDS.retirement, firstTeamId: id("f", 4), secondTeamId: id("f", 6), score: autumnScores.retirement, notPlayed: false },
          // Not played (#10): Alex and Chris never found a date.
          { id: FIXTURE_GAME_IDS.notPlayed, firstTeamId: id("f", 5), secondTeamId: id("f", 6), score: null, notPlayed: true },
        ],
        // Chris 4–2 and Jo 8–9 both won once, so games difference puts Chris first (#10).
        standings: [
          { teamId: id("f", 6), rank: 1, played: 1, won: 1, lost: 0, gamesFor: 4, gamesAgainst: 2, tied: false, move: "Stays" },
          { teamId: id("f", 4), rank: 2, played: 2, won: 1, lost: 1, gamesFor: 8, gamesAgainst: 9, tied: false, move: "Stays" },
          { teamId: id("f", 5), rank: 3, played: 1, won: 0, lost: 1, gamesFor: 5, gamesAgainst: 6, tied: false, move: "Stays" },
        ],
      },
    ],
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

/** Each League's own page, by id. */
export const fixtureLeaguePages: Record<string, Schemas["GetLeagueResponse"]> = {
  [winterSingles.id]: winterSinglesPage,
  [springDoubles.id]: springDoublesPage,
  [autumnSingles.id]: autumnSinglesPage,
};

const user = (
  n: number,
  email: string,
  players: Schemas["ClubMember"][],
  roles: Partial<Schemas["UserSummary"]> = {},
): Schemas["UserSummary"] => ({
  id: FIXTURE_USER_IDS[email] ?? id("a", n),
  email,
  manager: false,
  admin: false,
  adminBySetting: false,
  lastAdmin: false,
  players: players.map((player) => ({ id: player.playerId, name: player.name })),
  ...roles,
});

/**
 * Newest first, as the API lists them: everyone who holds a Player above
 * (Chris Lee's User was made when a Manager added him), then the two personas.
 */
export const fixtureUsers: Schemas["GetUsersResponse"] = {
  users: [
    user(8, ella.email, [ella]),
    user(9, nia.email, [nia]),
    user(3, tom.email, [tom]),
    user(4, priya.email, [priya]),
    user(5, chris.email, [chris]),
    user(6, jo.email, [jo]),
    user(7, alex.email, [alex, sam]),
    user(0, FIXTURE_MANAGER, [], { manager: true }),
    user(0, FIXTURE_ADMIN, [], { admin: true, adminBySetting: true }),
  ],
  page: { total: 9, offset: 0, limit: 50 },
};

/** Autumn Singles' Games (#38), as the fixture Manager sees them: holding no Player, so none of them theirs. */
const autumnGame = (gameId: string, first: Schemas["ClubMember"], second: Schemas["ClubMember"], firstTeam: number, secondTeam: number) => ({
  id: gameId,
  leagueId: autumnSingles.id,
  leagueName: autumnSingles.name,
  clubName: RIVERSIDE.name,
  type: autumnSingles.type,
  scoreFormat: autumnSingles.scoreFormat,
  // October is over and closed: its Players can't change a score, and the Manager can still override one.
  stage: { ...autumnStage, closed: true },
  division: 1,
  sides: [
    { side: 1, teamId: id("f", firstTeam), players: [{ playerId: first.playerId, name: first.name }] },
    { side: 2, teamId: id("f", secondTeam), players: [{ playerId: second.playerId, name: second.name }] },
  ],
  yourPlayerIds: [],
  timeZone: "Europe/London",
  today: "2026-11-02",
});

const snapshot = (score: Schemas["LadderGameScore"], playedOn: string) => ({ ...score, playedOn });

export const fixtureGames: Record<string, Schemas["GetGameResponse"]> = {
  // Alex entered it as Alex's win; Jo corrected the tiebreak the next morning.
  [FIXTURE_GAME_IDS.played]: {
    ...autumnGame(FIXTURE_GAME_IDS.played, jo, alex, 4, 5),
    score: { ...autumnScores.played, playedOn: "2026-10-08", submittedBy: alex.name, recordedByManager: false, submittedAt: "2026-10-08T19:40:00Z", version: 812 },
    notPlayed: false,
    history: [
      {
        kind: "Corrected",
        by: jo.name,
        at: "2026-10-09T07:15:00Z",
        before: snapshot(alexFirst, "2026-10-08"),
        after: snapshot(autumnScores.played, "2026-10-08"),
      },
      { kind: "Entered", by: alex.name, at: "2026-10-08T19:40:00Z", before: null, after: snapshot(alexFirst, "2026-10-08") },
    ],
  },
  [FIXTURE_GAME_IDS.retirement]: {
    ...autumnGame(FIXTURE_GAME_IDS.retirement, jo, chris, 4, 6),
    score: { ...autumnScores.retirement, playedOn: "2026-10-12", submittedBy: jo.name, recordedByManager: false, submittedAt: "2026-10-12T18:05:00Z", version: 640 },
    notPlayed: false,
    history: [{ kind: "Entered", by: jo.name, at: "2026-10-12T18:05:00Z", before: null, after: snapshot(autumnScores.retirement, "2026-10-12") }],
  },
  [FIXTURE_GAME_IDS.notPlayed]: { ...autumnGame(FIXTURE_GAME_IDS.notPlayed, alex, chris, 5, 6), score: null, notPlayed: true, history: [] },
};
