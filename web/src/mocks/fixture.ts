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

const whole = (total: number) => ({ total, offset: 0, limit: null });

export const fixtureClubs: Schemas["GetClubsResponse"] = { clubs: [RIVERSIDE, HILLSIDE], page: whole(2) };

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

/** Riverside has someone in every state; Hillside has no one, so its empty states show. */
export const fixtureMembers: Record<string, Schemas["GetClubMembersResponse"]> = {
  [RIVERSIDE.id]: {
    members: [
      alex,
      sam,
      jo,
      chris,
      priya,
      tom,
    ],
    invites: [
      { id: id("d", 1), email: "dana.price@example.com", joined: false, invitedAt: "2026-11-10T12:00:00Z" },
      { id: id("d", 2), email: jo.email, joined: true, invitedAt: "2026-10-01T08:00:00Z" },
    ],
    page: whole(6),
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
} as const;

export const fixtureLeagues: Record<string, Schemas["GetClubLeaguesResponse"]> = {
  [RIVERSIDE.id]: {
    leagues: [
      {
        id: id("e", 2),
        name: "Spring Doubles",
        type: "DoublesMix",
        structure: "Knockout",
        startDate: "2027-04-05",
        endDate: "2027-06-27",
        stageLength: { count: 2, unit: "Weeks" },
        stageRoll: null,
        scoreFormat: { gamesPerSet: 6, setsPerGame: 1, matchTiebreakDecider: false },
        participantCount: 0,
        pendingRequestCount: 0,
      },
      { ...winterSingles, participantCount: 2, pendingRequestCount: 1 },
    ],
    page: whole(2),
  },
  [HILLSIDE.id]: { leagues: [], page: whole(0) },
};

const inLeague = (who: Schemas["ClubMember"], joinedAt: string): Schemas["LeagueParticipantSummary"] => ({
  playerId: who.playerId,
  name: who.name,
  email: who.email,
  rating: who.rating,
  joinedAt,
});

const asked = (
  who: Schemas["ClubMember"],
  status: Schemas["LeagueJoinRequestStatus"],
  requestedAt: string,
  decidedAt: string | null,
): Schemas["LeagueJoinRequestSummary"] => ({
  playerId: who.playerId,
  name: who.name,
  email: who.email,
  rating: who.rating,
  status,
  requestedAt,
  decidedAt,
});

/**
 * Winter Singles: one request waiting, one turned down, two in, and two
 * members still to add. Requests in the API's order: waiting first, then the
 * latest decided.
 */
export const fixtureLeague: Schemas["GetLeagueResponse"] = {
  ...winterSingles,
  clubId: RIVERSIDE.id,
  clubName: RIVERSIDE.name,
  participants: [inLeague(alex, "2026-11-03T10:00:00Z"), inLeague(jo, "2026-11-05T19:20:00Z")],
  joinRequests: [
    asked(sam, "Pending", "2026-11-14T17:45:00Z", null),
    asked(chris, "Rejected", "2026-11-06T09:00:00Z", "2026-11-08T12:00:00Z"),
    asked(alex, "Approved", "2026-11-02T20:10:00Z", "2026-11-03T10:00:00Z"),
  ],
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
    user(3, tom.email, [tom]),
    user(4, priya.email, [priya]),
    user(5, chris.email, [chris]),
    user(6, jo.email, [jo]),
    user(7, alex.email, [alex, sam]),
    user(0, FIXTURE_MANAGER, [], { manager: true }),
    user(0, FIXTURE_ADMIN, [], { admin: true, adminBySetting: true }),
  ],
  page: { total: 7, offset: 0, limit: 50 },
};
