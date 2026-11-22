import { http as untypedHttp, passthrough } from "msw";
import { createOpenApiHttp } from "openapi-msw";
import type { components, paths } from "@/lib/api/schema";
import { DIVISION_SIZE, proposeDivisions } from "@/lib/league";
import {
  FIXTURE_ADMIN,
  FIXTURE_MANAGER,
  FIXTURE_USER_IDS,
  fixtureClubs,
  fixtureLeaguePages,
  fixtureLadders,
  fixtureLeagueStarts,
  fixtureLeagues,
  fixtureMembers,
  fixtureUsers,
} from "./fixture";

/**
 * Mocked API (ADR-0009), typed against the committed OpenAPI schema: a path,
 * body, or status the real API doesn't declare is a compile error here, so the
 * mocks can't drift from the contract silently.
 *
 * Mock auth has no inbox: any magic-link token redeems, and the session
 * remembers it — when the token is an email address, /auth/me reports that
 * email, and the email picks the starting roles (manager@…, admin@…).
 *
 * Players are kept in memory per session. Every sign-in is a new session and
 * so a new User with no Player, as a first sign-in is with the real API.
 */
const http = createOpenApiHttp<paths>({ baseUrl: process.env.API_URL });
const apiOrigin = new URL(process.env.API_URL ?? "http://api:8080").origin;

// Session tokens must survive a cookie round trip unencoded, like the real
// API's base64url tokens do — so the remembered value is base64url too.
const SESSION_PREFIX = "mock-session.";
const MOCK_CLUB = { id: "00000000-0000-4000-8000-0000000000c1", name: "Riverside Tennis Club" };

interface MockPlayer {
  id: string;
  name: string;
  gender: "Male" | "Female" | null;
  level: number | null;
  rating: number;
  /** Email of the session that made it. */
  email: string;
  /** Present once the Player has asked to join the one Club. */
  membership?: { approved: boolean; requestedAt: string };
}

/**
 * Players by session token, and so per sign-in. The Club's side is shared by
 * every session, as it has to be: a Manager approves in one session what a
 * Player asked for in another.
 */
const playersBySession = new Map<string, MockPlayer[]>();
/** Invites by email: used up (deleted) as one lets a Player in (#164). */
const invitedEmails = new Map<string, { id: string; invitedAt: string }>();
let invitesMade = 0;
let sessions = 0;
let playersMade = 0;

function sessionOf(request: Request): string | undefined {
  const bearer = request.headers.get("Authorization")?.replace(/^Bearer /, "");
  return bearer?.startsWith(SESSION_PREFIX) ? bearer : undefined;
}

function emailOf(request: Request): string {
  const encoded = (sessionOf(request) ?? "").slice(SESSION_PREFIX.length).split(".")[0];
  const remembered = Buffer.from(encoded, "base64url").toString();
  return remembered.includes("@") ? remembered.toLowerCase() : "player@example.com";
}

const nextPlayerId = () => `00000000-0000-4000-8000-${String((playersMade += 1)).padStart(12, "0")}`;

function playersOf(request: Request): MockPlayer[] {
  const session = sessionOf(request) ?? "";
  let players = playersBySession.get(session);
  if (!players) {
    // Signing in: whatever a Manager added for this email is theirs now.
    const email = emailOf(request);
    players = addedByManager.filter((player) => player.email === email);
    for (const player of players) addedByManager.splice(addedByManager.indexOf(player), 1);
    playersBySession.set(session, players);
  }
  return players;
}

/** Players a Manager added for an email nobody has signed in with yet. */
const addedByManager: MockPlayer[] = [];

const allPlayers = () => [...playersBySession.values()].flat().concat(addedByManager);

const validLevel = (level: unknown): level is number | null | undefined =>
  level === null || level === undefined || (typeof level === "number" && level >= 1 && level <= 5);

/**
 * Users by email, made the first time an email signs in. Roles start from the
 * email, so a test picks one by who it signs in as ("manager" in the local
 * part is a Manager, "admin" an Admin), and an Admin can then change them.
 * `admin@example.com` plays the Admin set in configuration: it can't be demoted.
 */
interface MockUser {
  id: string;
  manager: boolean;
  admin: boolean;
}

const CONFIGURED_ADMIN = "admin@example.com";
const usersByEmail = new Map<string, MockUser>();

function userOf(email: string): MockUser {
  let user = usersByEmail.get(email);
  if (!user) {
    const local = email.split("@")[0].split("+")[0];
    user = {
      id: FIXTURE_USER_IDS[email] ?? `00000000-0000-4000-8000-2${String(usersByEmail.size + 1).padStart(11, "0")}`,
      manager: local.includes("manager"),
      admin: local.includes("admin"),
    };
    usersByEmail.set(email, user);
  }
  return user;
}

/** Signed in as a fixture persona (fixture.ts): the Admin and Manager pages read the fixed world. */
const inFixture = (request: Request) => [FIXTURE_MANAGER, FIXTURE_ADMIN].includes(emailOf(request));

const userById = (id: string) => [...usersByEmail.values()].find((user) => user.id === id);

/**
 * Clubs an Admin created, by session. Kept to the session that made them, so
 * one test's Club never appears on another test's pages. Nobody can join one.
 */
const clubsBySession = new Map<string, { id: string; name: string }[]>();
let clubsMade = 0;

function clubsOf(request: Request) {
  const session = sessionOf(request) ?? "";
  let clubs = clubsBySession.get(session);
  if (!clubs) {
    clubs = [];
    clubsBySession.set(session, clubs);
  }
  return clubs;
}

type Schemas = components["schemas"];

/**
 * Leagues, shared by every session as the Club's side is: a Player asks to
 * join in one session what a Manager created in another. The Manage page
 * lists only the Leagues its own session created, so one test's League never
 * appears in another test's snapshot of it.
 */
interface MockLeague {
  id: string;
  clubId: string;
  session: string;
  name: string;
  type: Schemas["LeagueType"];
  structure: Schemas["LeagueStructure"];
  startDate: string;
  endDate: string;
  stageLength: { count: number; unit: Schemas["StageLengthUnit"] };
  stageRoll: Schemas["StageRoll"];
  scoreFormat: { gamesPerSet: number; setsPerGame: number; matchTiebreakDecider: boolean };
  participants: { playerId: string; joinedAt: string; teamId: string | null }[];
  requests: {
    playerId: string;
    /** The partner a doubles request names (#6); null for a Player asking alone. */
    partnerId: string | null;
    status: Schemas["LeagueJoinRequestStatus"];
    requestedAt: string;
    decidedAt: string | null;
  }[];
  teams: { id: string; createdAt: string }[];
  /** Teams per Division, for a Divisional League of Teams (ADR-0031). */
  divisionSize: number | null;
  /** Stage 1 once started (#7), its ladder (Team ids, top first) and its Division sizes. */
  stage: { number: number; startDate: string; endDate: string } | null;
  ladder: string[];
  divisions: number[];
}

/** The page block of a list that isn't paged: every match, from the start (ADR-0012). */
const wholeList = (total: number) => ({ total, offset: 0, limit: null });

const leagues: MockLeague[] = [];
let leaguesMade = 0;

const leagueById = (id: string) => leagues.find((league) => league.id === id);

/** As the API's LeagueTeams (#6): a Team of one in singles; unpaired in doubles; none in Foursomes. */
const teamSize = (type: Schemas["LeagueType"]) => (type.startsWith("Singles") ? 1 : type.startsWith("Doubles") ? 2 : 0);
let teamsMade = 0;

function newTeam(league: MockLeague) {
  const id = `00000000-0000-4000-8000-6${String((teamsMade += 1)).padStart(11, "0")}`;
  league.teams.push({ id, createdAt: new Date().toISOString() });
  return id;
}

function addParticipant(league: MockLeague, playerId: string, joinedAt: string) {
  league.participants.push({ playerId, joinedAt, teamId: teamSize(league.type) === 1 ? newTeam(league) : null });
}

/** Leaves the Team's members in the League, unpaired. */
function breakUp(league: MockLeague, teamId: string) {
  for (const participant of league.participants) {
    if (participant.teamId === teamId) participant.teamId = null;
  }
  league.teams = league.teams.filter((team) => team.id !== teamId);
}
/** As the API's StageDates.First (#7): from the League's start, one Stage length, no later than its end. */
function firstStage(league: MockLeague) {
  const [year, month, day] = league.startDate.split("-").map(Number);
  let next: Date;
  if (league.stageLength.unit === "Weeks") {
    next = new Date(Date.UTC(year, month - 1, day + 7 * league.stageLength.count));
  } else {
    // AddMonths clamps to the last day of a shorter month.
    const target = new Date(Date.UTC(year, month - 1 + league.stageLength.count, 1));
    const lastDay = new Date(Date.UTC(target.getUTCFullYear(), target.getUTCMonth() + 1, 0)).getUTCDate();
    next = new Date(Date.UTC(target.getUTCFullYear(), target.getUTCMonth(), Math.min(day, lastDay)));
  }
  next.setUTCDate(next.getUTCDate() - 1);
  const end = next.toISOString().slice(0, 10);
  return { number: 1, startDate: league.startDate, endDate: end > league.endDate ? league.endDate : end };
}

/** As the API's LeagueLadder.CannotStartAsync (#7). */
function cannotStart(league: MockLeague) {
  if (league.stage) return conflict("already-started", "This League has already started.");
  if (league.structure !== "Divisional") return conflict("knockout-not-yet", "A Knockout League can't start yet.");
  if (teamSize(league.type) === 0) return conflict("foursomes-not-yet", "A Foursomes League can't start yet.");
  return null;
}

/** A Team on the ladder is fixed once the League has started (#7). */
const onLadder = (league: MockLeague, teamId: string | null | undefined) => !!teamId && league.ladder.includes(teamId);

/** The Division of the Team at `position` (1 at the top), from the League's Division sizes (ADR-0031). */
const divisionOf = (league: MockLeague, position: number) => {
  let end = 0;
  for (const [index, size] of league.divisions.entries()) {
    end += size;
    if (position <= end) return index + 1;
  }
  return null;
};

const isClubMember = (player: MockPlayer | undefined, clubId: string) =>
  clubId === MOCK_CLUB.id && player?.membership?.approved === true;

/** As the API's LeagueEntry.IsInWaitingRequestAsync (#6): asking, or named as a partner. */
const inWaitingRequest = (league: MockLeague, playerId: string) =>
  league.requests.some(
    (joinRequest) => joinRequest.status === "Pending" && (joinRequest.playerId === playerId || joinRequest.partnerId === playerId),
  );

/**
 * As the API's LeagueEntry.ApproveAsync (#6): the Player joins at the request
 * time; with a partner named, both join as a Team, pairing a partner already
 * in without one. A refusal is returned with nothing changed.
 */
function approve(league: MockLeague, joinRequest: MockLeague["requests"][number]) {
  const player = (id: string) => allPlayers().find((candidate) => candidate.id === id);
  if (!isClubMember(player(joinRequest.playerId), league.clubId)) {
    return conflict("not-a-club-member", "This Player isn't a member of the League's Club.");
  }
  if (joinRequest.partnerId) {
    if (!isClubMember(player(joinRequest.partnerId), league.clubId)) {
      return conflict("partner-not-a-club-member", "The partner this Player named isn't a member of the League's Club.");
    }
    const pair = [joinRequest.playerId, joinRequest.partnerId];
    const already = league.participants.filter((participant) => pair.includes(participant.playerId));
    if (already.some((participant) => participant.teamId)) {
      return conflict("already-in-a-team", "One of the pair already has a partner in this League.");
    }
    const teamId = newTeam(league);
    for (const id of pair) {
      const participant = already.find((candidate) => candidate.playerId === id);
      if (participant) participant.teamId = teamId;
      else league.participants.push({ playerId: id, joinedAt: joinRequest.requestedAt, teamId });
    }
  } else if (!league.participants.some((participant) => participant.playerId === joinRequest.playerId)) {
    addParticipant(league, joinRequest.playerId, joinRequest.requestedAt);
  }
  joinRequest.status = "Approved";
  joinRequest.decidedAt = new Date().toISOString();
  return null;
}

/** The API's CreateLeagueRequest rules, field by field (CreateLeagueRequest.Validator). */
function leagueErrors(body: Schemas["CreateLeagueRequest"]): Record<string, string[]> {
  const errors: Record<string, string[]> = {};
  const add = (field: string, message: string) => (errors[field] = [...(errors[field] ?? []), message]);
  const sets = Number(body.scoreFormat.setsPerGame);
  if (body.name.trim().length === 0 || body.name.trim().length > 200) add("name", "A name is required.");
  if (body.type === "Foursomes" && body.structure === "Knockout")
    add("structure", "Foursomes is played in Divisions of 4 only, so a Foursomes League can't be Knockout.");
  if (!(body.endDate > body.startDate)) add("endDate", "The League has to end after it starts.");
  const count = Number(body.stageLength.count);
  if (!Number.isInteger(count) || count < 1 || count > 12)
    add("stageLength.count", "A Stage lasts from 1 to 12 weeks or months.");
  if (body.structure === "Divisional" && !body.stageRoll)
    add("stageRoll", "Choose whether each Stage closes automatically or when you close it.");
  if (body.structure === "Knockout" && body.stageRoll)
    add("stageRoll", "A Knockout League has no Relegation, so its Stages don't roll: leave this out.");
  if (![6, 9].includes(Number(body.scoreFormat.gamesPerSet))) add("scoreFormat.gamesPerSet", "A Set is played to 6 or 9 games.");
  if (![1, 3].includes(sets)) add("scoreFormat.setsPerGame", "A Game is one Set, or best of three.");
  if (body.divisionSize !== null && body.divisionSize !== undefined) {
    const size = Number(body.divisionSize);
    if (!Number.isInteger(size) || size < DIVISION_SIZE.min || size > DIVISION_SIZE.max)
      add("divisionSize", `A Division holds from ${DIVISION_SIZE.min} to ${DIVISION_SIZE.max} Teams.`);
    if (body.structure === "Knockout") add("divisionSize", "A Knockout League has no Divisions: leave this out.");
    if (body.type === "Foursomes") add("divisionSize", "A Foursomes Division is always 4 Players: leave this out.");
  }
  if (body.scoreFormat.matchTiebreakDecider && sets !== 3)
    add(
      "scoreFormat.matchTiebreakDecider",
      "A Match Tiebreak can only decide a best-of-three Game. A one-Set Game has no decider.",
    );
  return errors;
}

/** The fields every League contract shares. */
const leagueFacts = (league: MockLeague) => ({
  id: league.id,
  name: league.name,
  type: league.type,
  structure: league.structure,
  startDate: league.startDate,
  endDate: league.endDate,
  stageLength: league.stageLength,
  stageRoll: league.stageRoll,
  scoreFormat: league.scoreFormat,
  divisionSize: league.divisionSize,
});

const conflict = (type: string, title: string) => ({ type, title, status: 409 });

/** As the API's PlayerLeaguePlace (#7): the Player's Division and the other Teams in it; null off the ladder. */
function placeOf(league: MockLeague, playerId: string) {
  const teamId = league.participants.find((participant) => participant.playerId === playerId)?.teamId;
  const position = teamId ? league.ladder.indexOf(teamId) + 1 : 0;
  if (!league.stage || position === 0) return null;
  const division = divisionOf(league, position)!;
  const opponents = league.ladder
    .filter((other, index) => other !== teamId && divisionOf(league, index + 1) === division)
    .map((other) => ({
      teamId: other,
      names: league.participants
        .filter((participant) => participant.teamId === other)
        .sort((a, b) => a.joinedAt.localeCompare(b.joinedAt))
        .map((participant) => allPlayers().find((candidate) => candidate.id === participant.playerId)?.name ?? ""),
    }));
  return { division, opponents };
}

/** The other Player of this Player's Team, in a doubles League. */
function partnerName(league: MockLeague, playerId: string) {
  const teamId = league.participants.find((participant) => participant.playerId === playerId)?.teamId;
  if (!teamId || teamSize(league.type) !== 2) return null;
  const partnerId = league.participants.find((participant) => participant.teamId === teamId && participant.playerId !== playerId)?.playerId;
  return allPlayers().find((candidate) => candidate.id === partnerId)?.name ?? null;
}

const problemJson = { headers: { "Content-Type": "application/problem+json" } };
const invalid = (field: string) => ({ title: "Validation failed", status: 400, errors: { [field]: ["Invalid."] } });

export const handlers = [
  http.get("/clubs", ({ request, response }) => {
    if (inFixture(request)) {
      return response(200).json(fixtureClubs);
    }
    const clubs = [MOCK_CLUB, ...clubsOf(request)];
    return response(200).json({ clubs, page: wholeList(clubs.length) });
  }),

  http.post("/clubs", async ({ request, response }) => {
    const name = (await request.json()).name.trim();
    if (name.length === 0 || name.length > 200) {
      return response(400).json(invalid("name"), problemJson);
    }
    const club = { id: `00000000-0000-4000-8000-3${String((clubsMade += 1)).padStart(11, "0")}`, name };
    clubsOf(request).push(club);
    return response(201).json(club);
  }),

  http.put("/clubs/{clubId}", async ({ request, params, response }) => {
    const name = (await request.json()).name.trim();
    if (name.length === 0 || name.length > 200) {
      return response(400).json(invalid("name"), problemJson);
    }
    const club = clubsOf(request).find((candidate) => candidate.id === params.clubId);
    if (club) {
      club.name = name;
    } else if (params.clubId !== MOCK_CLUB.id) {
      return response(404).empty();
    }
    // The shared Club keeps its name: every other test's pages show it.
    return response(204).empty();
  }),

  // Asking to join: waits for a Manager, unless the email was invited (which uses the invite up) or the User
  // (a session, in the mock) already holds an approved member.
  http.put("/clubs/{clubId}/members/{playerId}", ({ request, params, response }) => {
    const player = playersOf(request).find((candidate) => candidate.id === params.playerId);
    if (!player || params.clubId !== MOCK_CLUB.id) {
      return response(404).empty();
    }
    const userIsIn = playersOf(request).some((held) => held.membership?.approved);
    if (!player.membership?.approved) {
      const admitted = invitedEmails.delete(player.email) || userIsIn;
      player.membership = { approved: admitted, requestedAt: player.membership?.requestedAt ?? new Date().toISOString() };
    }
    return response(204).empty();
  }),

  // The Manager's side. The mock doesn't check the role: the pages do.
  http.get("/clubs/{clubId}/members", ({ params, response }) => {
    const fixed = fixtureMembers[params.clubId];
    if (fixed) {
      return response(200).json(fixed);
    }
    if (params.clubId !== MOCK_CLUB.id) {
      return response(200).json({ members: [], invites: [], page: wholeList(0) });
    }
    const members = allPlayers()
      .filter((player) => player.membership)
      .map((player) => ({
        playerId: player.id,
        name: player.name,
        email: player.email,
        // Players a Manager added sit in no session until that email signs in.
        signedIn: !addedByManager.includes(player),
        approved: player.membership!.approved,
        gender: player.gender,
        level: player.level,
        rating: player.rating,
        requestedAt: player.membership!.requestedAt,
      }));
    return response(200).json({
      members,
      invites: [...invitedEmails].map(([email, invite]) => ({ ...invite, email })),
      page: wholeList(members.length),
    });
  }),

  http.put("/clubs/{clubId}/members/{playerId}/approval", ({ params, response }) => {
    const player = allPlayers().find((candidate) => candidate.id === params.playerId);
    if (!player?.membership) {
      return response(404).empty();
    }
    player.membership.approved = true;
    // The User (a session, in the mock) is in now: their other waiting requests are approved too (#164).
    const held = [...playersBySession.values()].find((players) => players.includes(player)) ?? [];
    for (const sibling of held) {
      if (sibling.membership) sibling.membership.approved = true;
    }
    return response(204).empty();
  }),

  http.delete("/clubs/{clubId}/members/{playerId}", ({ params, response }) => {
    const player = allPlayers().find((candidate) => candidate.id === params.playerId);
    if (player) {
      delete player.membership;
      // As the API: any invite left for the email goes too.
      invitedEmails.delete(player.email);
    }
    return response(204).empty();
  }),

  http.delete("/clubs/{clubId}/invites/{inviteId}", ({ params, response }) => {
    for (const [email, invite] of invitedEmails) {
      if (invite.id === params.inviteId) invitedEmails.delete(email);
    }
    return response(204).empty();
  }),

  http.post("/clubs/{clubId}/players", async ({ request, response }) => {
    const body = await request.json();
    const name = body.name.trim();
    const email = body.email.trim().toLowerCase();
    if (name.length === 0 || name.length > 100 || !email.includes("@")) {
      return response(400).json(invalid("name"), problemJson);
    }
    const player: MockPlayer = {
      id: nextPlayerId(),
      name,
      gender: null,
      level: null,
      rating: 1,
      email,
      membership: { approved: true, requestedAt: new Date().toISOString() },
    };
    // It waits for the next sign-in with that email. The mock's Players
    // belong to a session, not an email, so one already signed in doesn't
    // receive it as it would with the real API. In the Club now, so an
    // invite for the email is used up.
    addedByManager.push(player);
    invitedEmails.delete(email);
    return response(201).json({ playerId: player.id, name: player.name });
  }),

  http.post("/clubs/{clubId}/invites", async ({ request, response }) => {
    const email = (await request.json()).email.trim().toLowerCase();
    if (!email.includes("@")) {
      return response(400).json(invalid("email"), problemJson);
    }
    // Players the email already holds are let in now. The API then keeps no
    // invite; the mock keeps it anyway, because its Users are sessions and
    // the next sign-in with this address is a new one, whose first Player
    // uses the invite up.
    for (const player of allPlayers().filter((candidate) => candidate.email === email)) {
      player.membership = { approved: true, requestedAt: player.membership?.requestedAt ?? new Date().toISOString() };
    }
    if (!invitedEmails.has(email)) {
      invitedEmails.set(email, {
        id: `00000000-0000-4000-8000-1${String((invitesMade += 1)).padStart(11, "0")}`,
        invitedAt: new Date().toISOString(),
      });
    }
    return response(204).empty();
  }),

  http.get("/clubs/{clubId}/leagues", ({ request, params, response }) => {
    const fixed = fixtureLeagues[params.clubId];
    if (fixed) {
      return response(200).json(fixed);
    }
    const session = sessionOf(request) ?? "";
    const list = leagues
      .filter((league) => league.clubId === params.clubId && league.session === session)
      .sort((a, b) => b.startDate.localeCompare(a.startDate) || a.name.localeCompare(b.name))
      .map((league) => ({
        ...leagueFacts(league),
        participantCount: league.participants.length,
        pendingRequestCount: league.requests.filter((joinRequest) => joinRequest.status === "Pending").length,
        stage: league.stage,
      }));
    return response(200).json({ leagues: list, page: wholeList(list.length) });
  }),

  http.post("/clubs/{clubId}/leagues", async ({ request, params, response }) => {
    if (params.clubId !== MOCK_CLUB.id && !clubsOf(request).some((club) => club.id === params.clubId)) {
      return response(404).empty();
    }
    const body = await request.json();
    const errors = leagueErrors(body);
    if (Object.keys(errors).length > 0) {
      return response(400).json({ title: "Validation failed", status: 400, errors }, problemJson);
    }
    const league: MockLeague = {
      id: `00000000-0000-4000-8000-4${String((leaguesMade += 1)).padStart(11, "0")}`,
      clubId: params.clubId,
      session: sessionOf(request) ?? "",
      name: body.name.trim(),
      type: body.type,
      structure: body.structure,
      startDate: body.startDate,
      endDate: body.endDate,
      stageLength: { count: Number(body.stageLength.count), unit: body.stageLength.unit },
      stageRoll: body.stageRoll,
      divisionSize:
        body.structure === "Divisional" && body.type !== "Foursomes" ? Number(body.divisionSize ?? DIVISION_SIZE.default) : null,
      scoreFormat: {
        gamesPerSet: Number(body.scoreFormat.gamesPerSet),
        setsPerGame: Number(body.scoreFormat.setsPerGame),
        matchTiebreakDecider: body.scoreFormat.matchTiebreakDecider,
      },
      participants: [],
      requests: [],
      teams: [],
      stage: null,
      ladder: [],
      divisions: [],
    };
    leagues.push(league);
    return response(201).json({ id: league.id, name: league.name });
  }),

  http.get("/leagues/{leagueId}", ({ request, params, response }) => {
    const fixed = fixtureLeaguePages[params.leagueId];
    if (fixed) {
      return response(200).json(fixed);
    }
    const league = leagueById(params.leagueId);
    if (!league) {
      return response(404).empty();
    }
    const player = (id: string) => allPlayers().find((candidate) => candidate.id === id)!;
    const clubName =
      league.clubId === MOCK_CLUB.id
        ? MOCK_CLUB.name
        : (clubsOf(request).find((club) => club.id === league.clubId)?.name ?? "");
    const pendingFirst = [...league.requests].sort(
      (a, b) =>
        Number(a.status !== "Pending") - Number(b.status !== "Pending") ||
        (a.status === "Pending" ? a.requestedAt.localeCompare(b.requestedAt) : (b.decidedAt ?? "").localeCompare(a.decidedAt ?? "")),
    );
    return response(200).json({
      ...leagueFacts(league),
      clubId: league.clubId,
      clubName,
      participants: league.participants.map(({ playerId, joinedAt, teamId }) => ({
        playerId,
        name: player(playerId).name,
        email: player(playerId).email,
        rating: player(playerId).rating,
        joinedAt,
        gender: player(playerId).gender,
        teamId,
      })),
      joinRequests: pendingFirst.map(({ partnerId, ...joinRequest }) => ({
        ...joinRequest,
        name: player(joinRequest.playerId).name,
        email: player(joinRequest.playerId).email,
        rating: player(joinRequest.playerId).rating,
        gender: player(joinRequest.playerId).gender,
        partner: partnerId
          ? {
              playerId: partnerId,
              name: player(partnerId).name,
              email: player(partnerId).email,
              rating: player(partnerId).rating,
              gender: player(partnerId).gender,
            }
          : null,
      })),
      teams: league.teams.map((team) => {
        const members = league.participants.filter((participant) => participant.teamId === team.id);
        const position = league.ladder.indexOf(team.id) + 1 || null;
        return {
          id: team.id,
          playerIds: members.map((member) => member.playerId),
          rating: members.reduce((sum, member) => sum + player(member.playerId).rating, 0) / members.length,
          position,
          division: position ? divisionOf(league, position) : null,
        };
      }),
      stage: league.stage,
    });
  }),

  // Renaming (#153): only the name changes.
  http.put("/leagues/{leagueId}", async ({ request, params, response }) => {
    const name = (await request.json()).name.trim();
    if (name.length === 0 || name.length > 200) {
      return response(400).json(invalid("name"), problemJson);
    }
    const league = leagueById(params.leagueId);
    if (!league) {
      return response(404).empty();
    }
    league.name = name;
    return response(204).empty();
  }),

  http.put("/leagues/{leagueId}/participants/{playerId}", ({ params, response }) => {
    const league = leagueById(params.leagueId);
    const player = allPlayers().find((candidate) => candidate.id === params.playerId);
    if (!league || !player) {
      return response(404).empty();
    }
    if (league.participants.some((participant) => participant.playerId === player.id)) {
      return response(204).empty();
    }
    if (!isClubMember(player, league.clubId)) {
      return response(409).json(conflict("not-a-club-member", "This Player isn't a member of the League's Club."), problemJson);
    }
    // A pending request is approved as approving would: a named partner comes in too (#6).
    const pending = league.requests.find((joinRequest) => joinRequest.playerId === player.id && joinRequest.status === "Pending");
    if (!pending) {
      addParticipant(league, player.id, new Date().toISOString());
      return response(204).empty();
    }
    const refused = approve(league, pending);
    return refused ? response(409).json(refused, problemJson) : response(204).empty();
  }),

  // Removing before the start (#159): an approved request goes too, so they may ask again.
  http.delete("/leagues/{leagueId}/participants/{playerId}", ({ params, response }) => {
    const league = leagueById(params.leagueId);
    if (!league) {
      return response(404).empty();
    }
    const leaving = league.participants.find((participant) => participant.playerId === params.playerId);
    if (onLadder(league, leaving?.teamId)) {
      return response(409).json(conflict("on-the-ladder", "This Team is on the League's ladder."), problemJson);
    }
    if (leaving?.teamId) breakUp(league, leaving.teamId);
    league.participants = league.participants.filter((participant) => participant.playerId !== params.playerId);
    if (leaving) {
      league.requests = league.requests.filter(
        (joinRequest) => !(joinRequest.playerId === params.playerId && joinRequest.status === "Approved"),
      );
    }
    return response(204).empty();
  }),

  // Pairing (#6): two unpaired Players of a doubles League become a Team.
  http.post("/leagues/{leagueId}/teams", async ({ params, request, response }) => {
    const { firstPlayerId, secondPlayerId } = await request.json();
    const league = leagueById(params.leagueId);
    if (!league) {
      return response(404).empty();
    }
    if (firstPlayerId === secondPlayerId) {
      return response(400).json(invalid("secondPlayerId"), problemJson);
    }
    if (teamSize(league.type) !== 2) {
      return response(409).json(conflict("not-a-doubles-league", "Only a doubles League's Players have partners."), problemJson);
    }
    const pair = league.participants.filter(
      (participant) => [firstPlayerId, secondPlayerId].includes(participant.playerId) && participant.teamId === null,
    );
    if (pair.length !== 2) {
      return response(409).json(conflict("not-unpaired", "Both Players must be in the League without a partner."), problemJson);
    }
    const teamId = newTeam(league);
    for (const participant of pair) participant.teamId = teamId;
    return response(201).json({ id: teamId });
  }),

  http.delete("/leagues/{leagueId}/teams/{teamId}", ({ params, response }) => {
    const league = leagueById(params.leagueId);
    if (!league) {
      return response(404).empty();
    }
    if (teamSize(league.type) !== 2) {
      return response(409).json(conflict("not-a-doubles-league", "Only a doubles League's Players have partners."), problemJson);
    }
    if (onLadder(league, params.teamId)) {
      return response(409).json(conflict("on-the-ladder", "This Team is on the League's ladder."), problemJson);
    }
    breakUp(league, params.teamId);
    return response(204).empty();
  }),

  // Starting (#7): the proposal, then the Manager's confirmed ladder.
  http.get("/leagues/{leagueId}/start", ({ params, response }) => {
    const fixed = fixtureLeagueStarts[params.leagueId];
    if (fixed) {
      return response(200).json(fixed);
    }
    const league = leagueById(params.leagueId);
    if (!league) {
      return response(404).empty();
    }
    const refused = cannotStart(league);
    if (refused) {
      return response(409).json(refused, problemJson);
    }
    const player = (id: string) => allPlayers().find((candidate) => candidate.id === id)!;
    const competitors = league.teams
      .map((team) => {
        const members = league.participants
          .filter((participant) => participant.teamId === team.id)
          .sort((a, b) => a.joinedAt.localeCompare(b.joinedAt));
        return {
          teamId: team.id,
          players: members.map((member) => ({ playerId: member.playerId, name: player(member.playerId).name })),
          rating: members.reduce((sum, member) => sum + Number(player(member.playerId).rating), 0) / members.length,
          joinedAt: members[0]?.joinedAt ?? team.createdAt,
        };
      })
      .filter((competitor) => competitor.players.length > 0)
      .sort((a, b) => b.rating - a.rating || a.joinedAt.localeCompare(b.joinedAt) || a.teamId.localeCompare(b.teamId));
    const unpaired =
      teamSize(league.type) === 2
        ? league.participants
            .filter((participant) => participant.teamId === null)
            .map((participant) => ({ playerId: participant.playerId, name: player(participant.playerId).name }))
        : [];
    return response(200).json({
      competitors,
      unpaired,
      waitingRequestCount: league.requests.filter((joinRequest) => joinRequest.status === "Pending").length,
      divisionSize: league.divisionSize ?? DIVISION_SIZE.default,
      divisions: proposeDivisions(competitors.length, league.divisionSize ?? DIVISION_SIZE.default),
      firstStage: firstStage(league),
    });
  }),

  http.post("/leagues/{leagueId}/start", async ({ request, params, response }) => {
    const { ladder, divisions, leftOut, removed } = await request.json();
    const league = leagueById(params.leagueId);
    if (!league) {
      return response(404).empty();
    }
    if (ladder.length < 2 || new Set([...ladder, ...leftOut]).size !== ladder.length + leftOut.length) {
      return response(400).json(invalid("ladder"), problemJson);
    }
    const sizes = divisions.map(Number);
    if (sizes.length === 0 || sizes.some((size) => size < 2) || sizes.reduce((sum, size) => sum + size, 0) !== ladder.length) {
      return response(400).json(invalid("divisions"), problemJson);
    }
    const refused = cannotStart(league);
    if (refused) {
      return response(409).json(refused, problemJson);
    }
    const sameSet = (a: string[], b: string[]) => a.length === b.length && a.every((id) => b.includes(id));
    const unpaired =
      teamSize(league.type) === 2
        ? league.participants.filter((participant) => participant.teamId === null).map((participant) => participant.playerId)
        : [];
    if (!sameSet(league.teams.map((team) => team.id), [...ladder, ...leftOut]) || !sameSet(unpaired, removed)) {
      return response(409).json(conflict("league-changed", "The League has changed since the start was proposed."), problemJson);
    }
    league.participants = league.participants.filter((participant) => !removed.includes(participant.playerId));
    league.requests = league.requests.filter(
      (joinRequest) => !(removed.includes(joinRequest.playerId) && joinRequest.status === "Approved"),
    );
    league.stage = firstStage(league);
    league.ladder = [...ladder];
    league.divisions = sizes;
    return response(204).empty();
  }),

  // The ladder (#7): for the League's Players and Managers; a 404 for anyone else, as with the API.
  http.get("/leagues/{leagueId}/ladder", ({ request, params, response }) => {
    const fixed = fixtureLadders[params.leagueId];
    if (fixed) {
      return response(200).json(fixed);
    }
    const league = leagueById(params.leagueId);
    const user = userOf(emailOf(request));
    const inIt = playersOf(request).some((player) => league?.participants.some((participant) => participant.playerId === player.id));
    if (!league || !(inIt || user.manager || user.admin)) {
      return response(404).empty();
    }
    const facts = { id: league.id, name: league.name, clubName: MOCK_CLUB.name, type: league.type };
    if (!league.stage) {
      return response(200).json({ ...facts, stage: null, divisions: [] });
    }
    const competitors = league.ladder.map((teamId, index) => ({
      teamId,
      position: index + 1,
      players: league.participants
        .filter((participant) => participant.teamId === teamId)
        .sort((a, b) => a.joinedAt.localeCompare(b.joinedAt))
        .map((participant) => ({
          playerId: participant.playerId,
          name: allPlayers().find((candidate) => candidate.id === participant.playerId)?.name ?? "",
        })),
    }));
    // The Divisions at the sizes the Manager confirmed, each pair playing once (ADR-0031).
    let start = 0;
    const divisions = league.divisions.map((size, index) => {
      const teams = competitors.slice(start, start + size);
      start += size;
      const games = teams.flatMap((first, i) =>
        teams.slice(i + 1).map((second, j) => ({
          id: `${league.id.slice(0, -6)}${String(index + 1).padStart(2, "0")}${String(i).padStart(2, "0")}${String(i + j + 1).padStart(2, "0")}`,
          firstTeamId: first.teamId,
          secondTeamId: second.teamId,
        })),
      );
      return { number: index + 1, competitors: teams, games };
    });
    return response(200).json({ ...facts, stage: league.stage, divisions });
  }),

  http.post("/leagues/{leagueId}/join-requests", async ({ request, params, response }) => {
    const { playerId, partnerId } = await request.json();
    const player = playersOf(request).find((candidate) => candidate.id === playerId);
    const league = leagueById(params.leagueId);
    if (!player || !league || !isClubMember(player, league.clubId)) {
      return response(404).empty();
    }
    if (partnerId === player.id) {
      return response(400).json(invalid("partnerId"), problemJson);
    }
    if (league.participants.some((participant) => participant.playerId === player.id)) {
      return response(409).json(conflict("already-in-league", "This Player is already in the League."), problemJson);
    }
    if (league.requests.some((joinRequest) => joinRequest.playerId === player.id)) {
      return response(409).json(conflict("already-asked", "This Player has already asked to join the League."), problemJson);
    }
    if (inWaitingRequest(league, player.id)) {
      return response(409).json(conflict("named-as-partner", "Another Player has asked to join with this Player."), problemJson);
    }
    if (partnerId) {
      if (teamSize(league.type) !== 2) {
        return response(409).json(conflict("not-a-doubles-league", "Only a doubles League's Players have partners."), problemJson);
      }
      if (!isClubMember(allPlayers().find((candidate) => candidate.id === partnerId), league.clubId)) {
        return response(404).empty();
      }
      if (league.participants.some((participant) => participant.playerId === partnerId && participant.teamId)) {
        return response(409).json(conflict("partner-in-a-team", "That partner already has a partner in this League."), problemJson);
      }
      if (inWaitingRequest(league, partnerId)) {
        return response(409).json(conflict("partner-in-a-request", "That partner is already in a request to join this League."), problemJson);
      }
    }
    league.requests.push({
      playerId: player.id,
      partnerId: partnerId ?? null,
      status: "Pending",
      requestedAt: new Date().toISOString(),
      decidedAt: null,
    });
    return response(204).empty();
  }),

  // Taken back while it waits, by the Player who asked or the partner named (#6).
  http.delete("/leagues/{leagueId}/join-requests/{playerId}", ({ request, params, response }) => {
    const mine = playersOf(request).map((player) => player.id);
    const league = leagueById(params.leagueId);
    const joinRequest = league?.requests.find(
      (candidate) =>
        candidate.playerId === params.playerId &&
        (mine.includes(candidate.playerId) || (candidate.partnerId !== null && mine.includes(candidate.partnerId))),
    );
    if (!league || !joinRequest) {
      return response(404).empty();
    }
    if (joinRequest.status !== "Pending") {
      return response(409).json(conflict("join-request-decided", "A Manager has already decided this request."), problemJson);
    }
    league.requests = league.requests.filter((candidate) => candidate !== joinRequest);
    return response(204).empty();
  }),

  // Who a Player could ask to join a doubles League with (#6): names only.
  http.get("/leagues/{leagueId}/partners", ({ request, params, response }) => {
    const playerId = new URL(request.url).searchParams.get("playerId");
    const player = playersOf(request).find((candidate) => candidate.id === playerId);
    const league = leagueById(params.leagueId);
    if (!player || !league || !isClubMember(player, league.clubId)) {
      return response(404).empty();
    }
    if (teamSize(league.type) !== 2) {
      return response(409).json(conflict("not-a-doubles-league", "Only a doubles League's Players have partners."), problemJson);
    }
    const partners = allPlayers()
      .filter(
        (candidate) =>
          candidate.id !== player.id &&
          isClubMember(candidate, league.clubId) &&
          !league.participants.some((participant) => participant.playerId === candidate.id && participant.teamId) &&
          !inWaitingRequest(league, candidate.id),
      )
      .sort((a, b) => a.name.localeCompare(b.name) || a.id.localeCompare(b.id))
      .map((candidate) => ({ playerId: candidate.id, name: candidate.name }));
    return response(200).json({ partners, page: wholeList(partners.length) });
  }),

  http.put("/leagues/{leagueId}/join-requests/{playerId}/approval", ({ params, response }) => {
    const league = leagueById(params.leagueId);
    const joinRequest = league?.requests.find((candidate) => candidate.playerId === params.playerId);
    if (!league || !joinRequest) {
      return response(404).empty();
    }
    if (joinRequest.status === "Rejected") {
      return response(409).json(conflict("join-request-decided", "This request was already rejected."), problemJson);
    }
    const refused = joinRequest.status === "Pending" ? approve(league, joinRequest) : null;
    return refused ? response(409).json(refused, problemJson) : response(204).empty();
  }),

  http.put("/leagues/{leagueId}/join-requests/{playerId}/rejection", ({ params, response }) => {
    const joinRequest = leagueById(params.leagueId)?.requests.find((candidate) => candidate.playerId === params.playerId);
    if (!joinRequest) {
      return response(404).empty();
    }
    if (joinRequest.status === "Approved") {
      return response(409).json(conflict("join-request-decided", "This request was already approved."), problemJson);
    }
    if (joinRequest.status === "Pending") {
      joinRequest.status = "Rejected";
      joinRequest.decidedAt = new Date().toISOString();
    }
    return response(204).empty();
  }),

  http.get("/players/{playerId}/leagues", ({ request, params, response }) => {
    const player = playersOf(request).find((candidate) => candidate.id === params.playerId);
    if (!player) {
      return response(404).empty();
    }
    const list = leagues
      .filter(
        (league) =>
          league.participants.some((participant) => participant.playerId === player.id) ||
          isClubMember(player, league.clubId),
      )
      .sort((a, b) => b.startDate.localeCompare(a.startDate) || a.name.localeCompare(b.name))
      .map((league) => {
        const joinRequest = league.requests.find((candidate) => candidate.playerId === player.id);
        // A waiting request naming this Player as partner (#6).
        const namedIn = league.requests.find(
          (candidate) => candidate.partnerId === player.id && candidate.status === "Pending",
        );
        return {
          ...leagueFacts(league),
          clubId: league.clubId,
          clubName: MOCK_CLUB.name,
          participantCount: league.participants.length,
          joined: league.participants.some((participant) => participant.playerId === player.id),
          joinRequest: joinRequest
            ? {
                status: joinRequest.status,
                requestedAt: joinRequest.requestedAt,
                decidedAt: joinRequest.decidedAt,
                partnerName: allPlayers().find((candidate) => candidate.id === joinRequest.partnerId)?.name ?? null,
              }
            : null,
          partnerName: partnerName(league, player.id),
          stage: league.stage,
          place: placeOf(league, player.id),
          partnerRequest: namedIn
            ? {
                playerId: namedIn.playerId,
                name: allPlayers().find((candidate) => candidate.id === namedIn.playerId)?.name ?? "",
                requestedAt: namedIn.requestedAt,
              }
            : null,
        };
      });
    return response(200).json({ leagues: list, page: wholeList(list.length) });
  }),

  http.post("/auth/magic-link", ({ response }) => response(202).empty()),

  http.post("/auth/sessions", async ({ request, response }) => {
    const { token } = await request.json();
    return response(200).json({
      // The counter makes every sign-in its own session: base64url has no ".".
      token: `${SESSION_PREFIX}${Buffer.from(token).toString("base64url")}.${(sessions += 1)}`,
      expiresAt: new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString(),
    });
  }),

  http.get("/auth/me", ({ request, response }) => {
    if (!sessionOf(request)) {
      return response(401).json({ title: "Unauthorized", status: 401 }, problemJson);
    }
    const email = emailOf(request);
    const user = userOf(email);
    // An Admin is a Manager too, as with the real API.
    const roles = user.admin ? ["Admin", "Manager"] : user.manager ? ["Manager"] : [];
    return response(200).json({ userId: user.id, email, roles });
  }),

  // An Admin's side. The mock doesn't check the role: the pages do.
  // Paged and searchable like the API (#134): newest first, a whole-email match first.
  http.get("/users", ({ request, query, response }) => {
    if (inFixture(request)) {
      return response(200).json(fixtureUsers);
    }
    const search = (query.get("q") ?? "").trim().toLowerCase();
    const offset = Number(query.get("offset") ?? 0);
    const limit = Number(query.get("limit") ?? 50);
    const names = (email: string) =>
      allPlayers()
        .filter((player) => player.email === email)
        .map((player) => ({ id: player.id, name: player.name }));
    const matching = [...usersByEmail]
      .reverse()
      .filter(
        ([email]) =>
          !search ||
          email.includes(search) ||
          names(email).some((player) => player.name.toLowerCase().includes(search)),
      )
      .sort(([a], [b]) => Number(b === search) - Number(a === search));
    const users = matching.slice(offset, offset + limit).map(([email, user]) => ({
      id: user.id,
      email,
      manager: user.manager,
      admin: user.admin,
      adminBySetting: email === CONFIGURED_ADMIN,
      // Never: the configured Admin can always sign in.
      lastAdmin: false,
      players: names(email),
    }));
    return response(200).json({ users, page: { total: matching.length, offset, limit } });
  }),

  http.put("/users/{userId}/roles/{role}", ({ params, response }) => {
    const user = userById(params.userId);
    if (!user) {
      return response(404).empty();
    }
    if (params.role === "Admin") user.admin = true;
    else user.manager = true;
    return response(204).empty();
  }),

  http.delete("/users/{userId}/roles/{role}", ({ params, response }) => {
    const user = userById(params.userId);
    if (!user) {
      return response(404).empty();
    }
    if (params.role === "Admin") {
      if (usersByEmail.get(CONFIGURED_ADMIN) === user) {
        return response(409).json({ title: "The Admin role can't be removed from this User.", status: 409 }, problemJson);
      }
      user.admin = false;
    } else {
      user.manager = false;
    }
    return response(204).empty();
  }),

  http.delete("/auth/sessions/current", ({ response }) => response(204).empty()),

  http.get("/players", ({ request, response }) => {
    const players = playersOf(request).map((player) => ({
      id: player.id,
      name: player.name,
      gender: player.gender,
      level: player.level,
      clubs: player.membership ? [{ ...MOCK_CLUB, approved: player.membership.approved }] : [],
    }));
    return response(200).json({ players, page: wholeList(players.length) });
  }),

  http.post("/players", async ({ request, response }) => {
    const body = await request.json();
    const name = body.name.trim();
    if (name.length === 0 || name.length > 100 || !validLevel(body.level)) {
      return response(400).json(invalid("name"), problemJson);
    }
    const email = emailOf(request);
    // In the Club from the start if the email was invited (used up now), or
    // if the User already holds a member (#164). A mock User is a session:
    // the suite signs in many sessions with one persona address, and they
    // mustn't let each other's Players in.
    const admitted =
      invitedEmails.delete(email) || playersOf(request).some((held) => held.membership?.approved);
    const player: MockPlayer = {
      id: nextPlayerId(),
      name,
      gender: body.gender ?? null,
      level: body.level ?? null,
      rating: 1,
      email,
      membership: admitted ? { approved: true, requestedAt: new Date().toISOString() } : undefined,
    };
    playersOf(request).push(player);
    return response(201).json({ id: player.id, name: player.name });
  }),

  http.put("/players/{playerId}", async ({ request, params, response }) => {
    const body = await request.json();
    const name = body.name.trim();
    if (name.length === 0 || name.length > 100 || !validLevel(body.level)) {
      return response(400).json(invalid("name"), problemJson);
    }
    const player = playersOf(request).find((candidate) => candidate.id === params.playerId);
    if (!player) {
      return response(404).empty();
    }
    player.name = name;
    player.gender = body.gender ?? null;
    player.level = body.level ?? null;
    return response(204).empty();
  }),

  // No PUT /players/{playerId}/rating: Rating is hidden from the UI for now (#161), so nothing calls it.

  http.delete("/players/{playerId}/clubs/{clubId}", ({ request, params, response }) => {
    const player = playersOf(request).find((candidate) => candidate.id === params.playerId);
    if (!player) {
      return response(404).empty();
    }
    delete player.membership;
    return response(204).empty();
  }),

  // Last: only the API is mocked. Next.js also fetches its own origin
  // server-side (a server action's redirect loads the target's RSC payload),
  // and that must reach the real server. The predicate keeps this handler off
  // the API origin entirely: a handler that *matches* and returns nothing
  // counts as handled and lets the request through, so an API miss would
  // silently reach the network. Unmatched, it hits `onUnhandledFrame: "error"`
  // — a gap in the mocks (MSW docs, custom predicate).
  untypedHttp.all(
    ({ request }) => new URL(request.url).origin !== apiOrigin,
    () => passthrough(),
  ),
];
