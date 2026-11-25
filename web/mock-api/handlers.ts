import { randomBytes } from "node:crypto";
import { HttpResponse } from "msw";
import { createOpenApiHttp } from "openapi-msw";
import type { components, paths } from "@/lib/api/schema";
import { isJoinCode } from "@/lib/join";
import { DIVISION_SIZE, proposeDivisions, relegate } from "@/lib/league";
import { checkScore, isMatchTiebreak, scoreFormat, type ScoreSet, type Side } from "@/lib/score";
import { gamesOf, openTiesOf, standingsOf } from "./standings";
import {
  FIXTURE_ADMIN,
  FIXTURE_MANAGER,
  FIXTURE_USER_IDS,
  fixtureClubs,
  fixtureGames,
  fixtureLeaguePages,
  fixtureLadders,
  fixtureLeagueStarts,
  fixtureLeagues,
  fixtureMembers,
  fixtureUsers,
} from "./fixture";

/**
 * The mock API's answers (ADR-0009), typed against the committed OpenAPI
 * schema: a path, body, or status the real API doesn't declare is a compile
 * error here, so the mock can't drift from the contract silently. server.ts
 * serves them.
 *
 * Mock auth has no inbox: the sign-in link goes to this server's log, any
 * magic-link token redeems, and the session remembers it. When the token is an
 * email address, /auth/me reports that email, and the email picks the starting
 * roles (manager@…, admin@…).
 *
 * Players are kept in memory per session. Every sign-in is a new session and
 * so a new User with no Player, as a first sign-in is with the real API.
 */
const http = createOpenApiHttp<paths>();

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

/**
 * Deleted Users (#116), by email: the session counter when each was deleted.
 * Their sessions made before then are over; a later sign-in is a new account.
 */
const deletedAtSession = new Map<string, number>();

function sessionOf(request: Request): string | undefined {
  const bearer = request.headers.get("Authorization")?.replace(/^Bearer /, "");
  if (!bearer?.startsWith(SESSION_PREFIX)) return undefined;
  const [encoded, count] = bearer.slice(SESSION_PREFIX.length).split(".");
  const email = Buffer.from(encoded, "base64url").toString().toLowerCase();
  const deletedAt = deletedAtSession.get(email);
  return deletedAt !== undefined && Number(count) <= deletedAt ? undefined : bearer;
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
 * part is a manager of the shared Club, "league-manager" a League manager there
 * (#220), "admin" an Admin), and an Admin or a Club's manager can then change
 * them.
 * `admin@example.com` plays the Admin set in configuration: it can't be demoted.
 */
interface MockUser {
  id: string;
  /** Their role in Clubs: a manager, or a League manager who creates Leagues there (#110, #220). An Admin manages every one. */
  clubs: Map<string, "Manager" | "LeagueManager">;
  /** The Leagues they run: assigned to them, or created as a League manager (#220). */
  leagues: Set<string>;
  admin: boolean;
}

const CONFIGURED_ADMIN = "admin@example.com";
const usersByEmail = new Map<string, MockUser>();

/** Each email's User id, made when first asked for: a Player a Manager added has one before anyone signs in with it. */
const userIds = new Map<string, string>();
let usersMade = 0;

function userIdOf(email: string): string {
  let id = FIXTURE_USER_IDS[email] ?? userIds.get(email);
  if (!id) {
    // Counted, not sized: a deleted User's id is never given out again.
    id = `00000000-0000-4000-8000-2${String((usersMade += 1)).padStart(11, "0")}`;
  }
  userIds.set(email, id);
  return id;
}

function userOf(email: string): MockUser {
  let user = usersByEmail.get(email);
  if (!user) {
    const local = email.split("@")[0].split("+")[0];
    user = {
      id: userIdOf(email),
      clubs: new Map(
        local.includes("league-manager")
          ? [[MOCK_CLUB.id, "LeagueManager"]]
          : local.includes("manager")
            ? [[MOCK_CLUB.id, "Manager"]]
            : [],
      ),
      leagues: new Set(),
      admin: local.includes("admin"),
    };
    usersByEmail.set(email, user);
  }
  return user;
}

/** Signed in as a fixture persona (fixture.ts): the Admin and Manager pages read the fixed world. */
const inFixture = (request: Request) => [FIXTURE_MANAGER, FIXTURE_ADMIN].includes(emailOf(request));

/** Whether the caller runs this Club, as the API's ManagerAccess says it (#110). */
function runsClub(request: Request, clubId: string) {
  const user = userOf(emailOf(request));
  return user.admin || user.clubs.get(clubId) === "Manager";
}

/** Whether the caller runs this League: its Club's managers do, and whoever it is assigned to (#220). */
function runsLeague(request: Request, league: MockLeague) {
  return runsClub(request, league.clubId) || userOf(emailOf(request)).leagues.has(league.id);
}

/** Whether the caller has any standing in this Club (#220): a role there, or a League of its to run. */
function hasStanding(request: Request, clubId: string) {
  const user = userOf(emailOf(request));
  return user.admin || user.clubs.has(clubId) || leagues.some((league) => league.clubId === clubId && user.leagues.has(league.id));
}

/** Who a League is assigned to (#220), by the Player each holds in its Club: none when its Club's managers run it. */
function managersOf(league: MockLeague) {
  return [...usersByEmail]
    .filter(([, user]) => user.leagues.has(league.id))
    .map(([email, user]) => ({
      userId: user.id,
      email,
      name: allPlayers().find((player) => player.email === email && player.membership?.approved)?.name ?? null,
    }));
}

/** Whether a User has a Player in the shared Club, as the API asks of anyone given a role or a League there (#220). */
const playsInTheClub = (email: string) => allPlayers().some((player) => player.email === email && player.membership?.approved);

/** The email of this User, who may not have signed in yet: a Player a Manager added has one. */
const emailOfUser = (id: string) => [...userIds].find(([, userId]) => userId === id)?.[0];

const userById = (id: string) => [...usersByEmail.values()].find((user) => user.id === id);

/**
 * Each Club's join link (#222), by Club id, with the Club's name for the page
 * it opens. Every link starts off, as in the API; turning one on or resetting
 * it makes a new code.
 */
const joinLinks = new Map<string, { code: string; name: string }>();

/** Fixed data's Riverside link, so its Manage page snapshot is the same every run. */
const FIXTURE_JOIN_CODE = "FixedJoinCodeForSnapsh";

function clubOfJoinCode(code: string) {
  for (const [id, link] of joinLinks) {
    if (link.code === code) return { id, name: link.name };
  }
  return undefined;
}

/**
 * Clubs an Admin created, by session. Kept to the session that made them, so
 * one test's Club never appears on another test's pages. Nobody can join one.
 */
const clubsBySession = new Map<string, { id: string; name: string }[]>();
let clubsMade = 0;

/** A Club's name, whichever session made it: an Admin lists what anyone manages (#110). */
const clubNameOf = (clubId: string) =>
  [MOCK_CLUB, ...[...clubsBySession.values()].flat()].find((club) => club.id === clubId)?.name ?? "";

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
  /** The current Stage once started (#7), its ladder (Team ids, top first) and its Division sizes. */
  stage: { number: number; startDate: string; endDate: string } | null;
  ladder: string[];
  divisions: number[];
  /** The current Stage has closed: it was the League's last, so the League has ended (#10). */
  stageClosed: boolean;
  /** Stages closed before the current one, with their ladders, so their Games stay (#10). */
  pastStages: MockStage[];
}

/** A Stage and its ladder: the current one is the League's own fields, closed ones are kept as these. */
interface MockStage {
  stage: { number: number; startDate: string; endDate: string };
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
  return { number: 1, ...stageFrom(league, league.startDate) };
}

/** As the API's StageDates.Next (#10): from the day after the last Stage ended; none once the League has no day left. */
function nextStage(league: MockLeague, previousEnd: string) {
  const day = new Date(`${previousEnd}T00:00:00Z`);
  day.setUTCDate(day.getUTCDate() + 1);
  const start = day.toISOString().slice(0, 10);
  return start > league.endDate ? null : stageFrom(league, start);
}

/** One Stage length from `start`, ending no later than the League. */
function stageFrom(league: MockLeague, start: string) {
  const [year, month, day] = start.split("-").map(Number);
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
  return { startDate: start, endDate: end > league.endDate ? league.endDate : end };
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

/** A Team's Players by join time, as the API names them. */
const teamPlayers = (league: MockLeague, teamId: string) =>
  league.participants
    .filter((participant) => participant.teamId === teamId)
    .sort((a, b) => a.joinedAt.localeCompare(b.joinedAt))
    .map((participant) => ({
      playerId: participant.playerId,
      name: allPlayers().find((candidate) => candidate.id === participant.playerId)?.name ?? "",
    }));

/**
 * A Game's id, by its League, Stage and pair of Teams: so a mid-Stage move (#10),
 * which shifts places, leaves every other Game, and its Score, where it was.
 */
const gameIds = new Map<string, string>();
let gamesMade = 0;
function gameIdOf(league: MockLeague, stage: number, first: string, second: string) {
  const key = `${league.id}:${stage}:${first}:${second}`;
  let id = gameIds.get(key);
  if (!id) {
    id = `00000000-0000-4000-8000-7${String((gamesMade += 1)).padStart(11, "0")}`;
    gameIds.set(key, id);
  }
  return id;
}

/** The current Stage as a MockStage; none before the start. */
const currentOf = (league: MockLeague): MockStage | null =>
  league.stage ? { stage: league.stage, ladder: league.ladder, divisions: league.divisions } : null;

/** A Stage's Divisions at the sizes the Manager confirmed, each pair playing once (ADR-0031); the current Stage's by default. */
function ladderOf(league: MockLeague, of: MockStage | null = currentOf(league)) {
  if (!of) return [];
  const competitors = of.ladder.map((teamId, index) => ({ teamId, position: index + 1, players: teamPlayers(league, teamId) }));
  let start = 0;
  return of.divisions.map((size, index) => {
    const teams = competitors.slice(start, start + size);
    start += size;
    const games = teams.flatMap((first, i) =>
      teams.slice(i + 1).map((second) => ({
        id: gameIdOf(league, of.stage.number, first.teamId, second.teamId),
        firstTeamId: first.teamId,
        secondTeamId: second.teamId,
      })),
    );
    return { number: index + 1, competitors: teams, games };
  });
}

/** A League Game by id, with its League, Stage (closed or not) and Division (#38, #10). */
function gameById(gameId: string) {
  for (const league of leagues) {
    const current = currentOf(league);
    for (const of of [...league.pastStages, ...(current ? [current] : [])]) {
      const closed = of !== current || league.stageClosed;
      for (const division of ladderOf(league, of)) {
        const game = division.games.find((candidate) => candidate.id === gameId);
        if (game) return { league, stage: { ...of.stage, closed }, division: division.number, ...game };
      }
    }
  }
  return null;
}

/** What a Score said at one moment (#38). */
interface MockScoreValue {
  playedOn: string;
  sets: ScoreSet[];
  winner: Side;
  retired: boolean;
}

/** Scores by Game id (#38): shared by every session, like the Leagues, with their history, the latest first. */
interface MockScore extends MockScoreValue {
  /** None when a Manager holding no Player recorded it (#10). */
  submittedBy: string | null;
  recordedByManager: boolean;
  submittedAt: string;
  version: number;
  history: { kind: Schemas["ScoreChangeKind"]; by: string | null; at: string; before: MockScoreValue | null; after: MockScoreValue }[];
}
const scores = new Map<string, MockScore>();
let scoreVersions = 0;
/** Games a Manager marked not played (#10). */
const voided = new Set<string>();

const snapshotOf = (league: MockLeague, value: MockScoreValue) => ({ ...value, sets: setScores(league, value.sets) });

/** As the API's GameScores.Check (#38): the winner, and the problems by wire name. */
function checkEntry(
  league: MockLeague,
  stage: { number: number; startDate: string },
  body: { playedOn: string; sets: { first: number | string; second: number | string; firstTiebreak?: number | string | null; secondTiebreak?: number | string | null }[]; retirementWinner?: number | string | null },
) {
  const value = (point: number | string | null | undefined) => (point === null || point === undefined ? null : Number(point));
  const sets: ScoreSet[] = body.sets.map((set) => ({
    first: Number(set.first),
    second: Number(set.second),
    firstTiebreak: value(set.firstTiebreak),
    secondTiebreak: value(set.secondTiebreak),
  }));
  const declared = value(body.retirementWinner) as Side | null;
  const check = checkScore(scoreFormat(league.scoreFormat), sets, declared);
  const errors: Record<string, string[]> = check.ok ? {} : { [check.field]: [check.message] };
  if (body.playedOn < stage.startDate) {
    const started = new Intl.DateTimeFormat("en-GB", { day: "numeric", month: "short", year: "numeric", timeZone: "UTC" }).format(
      new Date(`${stage.startDate}T00:00:00Z`),
    );
    errors.playedOn = [`Stage ${stage.number} started on ${started}: the game can't have been played before that.`];
  } else if (body.playedOn > clubToday()) {
    errors.playedOn = ["The date played can't be in the future."];
  }
  const after: MockScoreValue | null = check.ok
    ? { playedOn: body.playedOn, sets, winner: check.winner, retired: declared !== null }
    : null;
  return { errors, after: Object.keys(errors).length === 0 ? after : null };
}

/** As the API's GameScores.ChangeAsync (#38): a stale version is refused; a change that changes nothing records nothing. */
function changeScore(score: MockScore, version: number, after: MockScoreValue, kind: Schemas["ScoreChangeKind"], by: string | null) {
  const before: MockScoreValue = { playedOn: score.playedOn, sets: score.sets, winner: score.winner, retired: score.retired };
  if (JSON.stringify(before) === JSON.stringify(after)) return true;
  if (version !== score.version) return false;
  Object.assign(score, after, { version: (scoreVersions += 1) });
  score.history.unshift({ kind, by, at: new Date().toISOString(), before, after });
  return true;
}

const setScores = (league: MockLeague, sets: ScoreSet[]) =>
  sets.map((set, index) => ({ ...set, matchTiebreak: isMatchTiebreak(scoreFormat(league.scoreFormat), index) }));

/** Today in the Club's time zone (ADR-0016): every mock Club is in London. */
const clubToday = () =>
  new Intl.DateTimeFormat("en-CA", { timeZone: "Europe/London", year: "numeric", month: "2-digit", day: "2-digit" }).format(new Date());

/** As the API's StageLadder.BuildAsync (#7, #10): the current Stage's Divisions, with their Games' Scores and Standings. */
function ladderDivisions(league: MockLeague) {
  const ladder = ladderOf(league);
  const format = scoreFormat(league.scoreFormat);
  const divisions = ladder.map(({ number, competitors, games }) => ({
    number,
    competitors,
    // As the API's Standings (#10), from the Scores so far.
    standings: standingsOf(
      competitors.map((competitor) => competitor.teamId),
      games.map(({ id, firstTeamId, secondTeamId }) => {
        const score = scores.get(id);
        const [firstGames, secondGames] = score ? gamesOf(format, score.sets) : [0, 0];
        return { first: firstTeamId, second: secondTeamId, result: score ? { winner: score.winner, firstGames, secondGames } : null };
      }),
      number,
      ladder.length,
    ),
    games: games.map(({ id, firstTeamId, secondTeamId }) => {
      const score = scores.get(id);
      return {
        id,
        firstTeamId,
        secondTeamId,
        score: score ? { winner: score.winner, retired: score.retired, sets: setScores(league, score.sets) } : null,
        notPlayed: voided.has(id),
      };
    }),
  }));
  return divisions;
}

/** As the API: the Games nobody scored or voided, and the ties that decide who moves. */
function awaitingOf(divisions: ReturnType<typeof ladderDivisions>) {
  return {
    unscoredGameIds: divisions.flatMap((division) => division.games.filter((game) => !game.score && !game.notPlayed).map((game) => game.id)),
    ties: divisions.flatMap((division) =>
      openTiesOf(division.standings, division.number, divisions.length).map((teamIds) => ({ division: division.number, teamIds })),
    ),
  };
}

/** Teams in the League but off its current ladder, complete for its Type: those a close can add (#10). */
function waitingTeams(league: MockLeague) {
  const size = teamSize(league.type);
  return league.teams
    .filter((team) => !league.ladder.includes(team.id))
    .filter((team) => league.participants.filter((participant) => participant.teamId === team.id).length === size)
    .map((team) => team.id);
}

/** Who holds a role in a Club (#110, #220): its managers first, then its League managers. */
const rolesIn = (clubId: string) =>
  [...usersByEmail]
    .filter(([, user]) => user.clubs.has(clubId))
    .map(([email, user]) => ({ userId: user.id, email, role: user.clubs.get(clubId)! }))
    .sort((a, b) => Number(a.role === "LeagueManager") - Number(b.role === "LeagueManager") || a.email.localeCompare(b.email));

const notInClub = { type: "no-player-in-club", title: "They have no player in this Club, so they can't be its League manager.", status: 409 };

const problemJson = { headers: { "Content-Type": "application/problem+json" } };
const invalid = (field: string) => ({ title: "Validation failed", status: 400, errors: { [field]: ["Invalid."] } });

export const handlers = [
  http.get("/clubs", ({ request, response }) => {
    if (inFixture(request)) {
      return response(200).json(fixtureClubs);
    }
    const clubs = [MOCK_CLUB, ...clubsOf(request)]
      .filter((club) => hasStanding(request, club.id))
      .map(({ id, name }) => ({
        id,
        name,
        canManage: runsClub(request, id),
        canCreateLeagues: userOf(emailOf(request)).admin || userOf(emailOf(request)).clubs.has(id),
      }));
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

  // Asking to join, through the Club's join link (#222): waits for a Manager, unless the email was invited (which
  // uses the invite up) or the User (a session, in the mock) already holds an approved member. Only the shared Club
  // takes members in the mock.
  http.put("/join-links/{code}/members/{playerId}", ({ request, params, response }) => {
    const player = playersOf(request).find((candidate) => candidate.id === params.playerId);
    if (!player || clubOfJoinCode(params.code)?.id !== MOCK_CLUB.id) {
      return response(404).empty();
    }
    const userIsIn = playersOf(request).some((held) => held.membership?.approved);
    if (!player.membership?.approved) {
      const admitted = invitedEmails.delete(player.email) || userIsIn;
      player.membership = { approved: admitted, requestedAt: player.membership?.requestedAt ?? new Date().toISOString() };
    }
    return response(204).empty();
  }),

  // The Club a join link is for; a link that's off, reset or unknown is a 404 alike.
  http.get("/join-links/{code}", ({ params, response }) => {
    const club = clubOfJoinCode(params.code);
    return club ? response(200).json({ clubId: club.id, clubName: club.name }) : response(404).empty();
  }),

  // A Manager's side of the link. Fixed data: Riverside's is on, Hillside's off, as the snapshots show.
  http.get("/clubs/{clubId}/join-link", ({ request, params, response }) => {
    if (inFixture(request)) {
      return response(200).json({ code: params.clubId === fixtureClubs.clubs[1].id ? FIXTURE_JOIN_CODE : null });
    }
    return response(200).json({ code: joinLinks.get(params.clubId)?.code ?? null });
  }),

  // Turning on keeps a link that's on, as the API does, so tests sharing the Club share its link.
  http.put("/clubs/{clubId}/join-link", ({ request, params, response }) => {
    const club = [MOCK_CLUB, ...clubsOf(request)].find((candidate) => candidate.id === params.clubId);
    if (!club) {
      return response(404).empty();
    }
    const link = joinLinks.get(club.id) ?? { code: randomBytes(16).toString("base64url"), name: club.name };
    joinLinks.set(club.id, link);
    return response(200).json({ code: link.code });
  }),

  http.post("/clubs/{clubId}/join-link/reset", ({ request, params, response }) => {
    const club = [MOCK_CLUB, ...clubsOf(request)].find((candidate) => candidate.id === params.clubId);
    if (!club) {
      return response(404).empty();
    }
    const code = randomBytes(16).toString("base64url");
    joinLinks.set(club.id, { code, name: club.name });
    return response(200).json({ code });
  }),

  http.delete("/clubs/{clubId}/join-link", ({ params, response }) => {
    joinLinks.delete(params.clubId);
    return response(204).empty();
  }),

  // The Manager's side. The mock doesn't check the role: the pages do.
  http.get("/clubs/{clubId}/members", ({ params, response }) => {
    const fixed = fixtureMembers[params.clubId];
    if (fixed) {
      return response(200).json(fixed);
    }
    if (params.clubId !== MOCK_CLUB.id) {
      return response(200).json({ members: [], invites: [], roles: rolesIn(params.clubId), page: wholeList(0) });
    }
    const members = allPlayers()
      .filter((player) => player.membership)
      .map((player) => ({
        playerId: player.id,
        userId: userIdOf(player.email),
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
      roles: rolesIn(params.clubId),
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
    // As the API (#224): the address's Players come in too, and a name it holds already adds nobody new.
    const held = allPlayers().filter((candidate) => candidate.email === email);
    for (const other of held) {
      other.membership = { approved: true, requestedAt: other.membership?.requestedAt ?? new Date().toISOString() };
    }
    const same = held.find((candidate) => candidate.name.toLowerCase() === name.toLowerCase());
    if (same) {
      invitedEmails.delete(email);
      return response(200).json({ playerId: same.id, name: same.name });
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
    // A Club's manager sees the Leagues their own session made, so one test's League is never in another's list;
    // a League manager sees the ones they run (#220).
    const user = userOf(emailOf(request));
    const list = leagues
      .filter(
        (league) =>
          league.clubId === params.clubId &&
          (runsClub(request, params.clubId) ? league.session === session : user.leagues.has(league.id)),
      )
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
      stageClosed: false,
      pastStages: [],
    };
    leagues.push(league);
    // A League manager runs what they create (#220); a Club's manager needs no assignment.
    if (!runsClub(request, params.clubId)) userOf(emailOf(request)).leagues.add(league.id);
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
    // As the API: who runs it (#220), and someone who can only see it is refused.
    if (!runsLeague(request, league)) {
      return response(403).json({ title: "Forbidden", status: 403 }, problemJson);
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
      managers: managersOf(league).map(({ email, ...manager }) => ({ ...manager, email: runsClub(request, league.clubId) ? email : null })),
      canAssignManagers: runsClub(request, league.clubId),
    });
  }),

  // Who could be added (#220): the Club's members who aren't in it, with emails only for its Club's managers.
  http.get("/leagues/{leagueId}/addable-players", ({ request, params, response }) => {
    const fixed = fixtureLeaguePages[params.leagueId];
    if (fixed) {
      const inLeague = new Set(fixed.participants.map((participant) => participant.playerId));
      const players = fixtureMembers[fixed.clubId].members
        .filter((member) => member.approved && !inLeague.has(member.playerId))
        .map(({ playerId, name, email }) => ({ playerId, name, email }));
      return response(200).json({ players, page: wholeList(players.length) });
    }
    const league = leagueById(params.leagueId);
    if (!league || !runsLeague(request, league)) {
      return response(404).empty();
    }
    const emails = runsClub(request, league.clubId);
    const players = allPlayers()
      .filter((player) => player.membership?.approved && !league.participants.some((participant) => participant.playerId === player.id))
      .sort((a, b) => a.name.localeCompare(b.name))
      .map((player) => ({ playerId: player.id, name: player.name, email: emails ? player.email : null }));
    return response(200).json({ players, page: wholeList(players.length) });
  }),

  // Who runs a League (#220): its Club's managers assign and take back.
  http.put("/leagues/{leagueId}/managers/{userId}", ({ request, params, response }) => {
    const league = leagueById(params.leagueId);
    const email = emailOfUser(params.userId);
    if (!league || !runsClub(request, league.clubId) || !email) {
      return response(404).empty();
    }
    if (!playsInTheClub(email)) {
      return response(409).json(notInClub, problemJson);
    }
    userOf(email).leagues.add(league.id);
    return response(204).empty();
  }),

  http.delete("/leagues/{leagueId}/managers/{userId}", ({ request, params, response }) => {
    const league = leagueById(params.leagueId);
    const user = userById(params.userId);
    if (!league || !runsClub(request, league.clubId) || !user) {
      return response(404).empty();
    }
    user.leagues.delete(league.id);
    return response(204).empty();
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
    const yourPlayerIds = playersOf(request)
      .filter((player) => league?.participants.some((participant) => participant.playerId === player.id))
      .map((player) => player.id);
    if (!league || !(yourPlayerIds.length > 0 || runsLeague(request, league))) {
      return response(404).empty();
    }
    const facts = {
      id: league.id,
      name: league.name,
      clubName: MOCK_CLUB.name,
      type: league.type,
      yourPlayerIds,
      managers: managersOf(league).map(({ name }) => ({ name })),
      canManage: runsLeague(request, league),
    };
    if (!league.stage) {
      return response(200).json({ ...facts, stage: null, divisions: [], heldUp: false, awaiting: { unscoredGameIds: [], ties: [] }, ended: false });
    }
    const divisions = ladderDivisions(league);
    // As the API (#10): what holds up the close, and whether the deadline has passed.
    const awaiting = league.stageClosed ? { unscoredGameIds: [], ties: [] } : awaitingOf(divisions);
    const heldUp = !league.stageClosed && clubToday() > league.stage.endDate;
    return response(200).json({ ...facts, stage: league.stage, divisions, heldUp, awaiting, ended: league.stageClosed });
  }),

  // As the API's MoveTeam (#10): a Team on the ladder to another Division, mid-Stage.
  http.post("/leagues/{leagueId}/moves", async ({ request, params, response }) => {
    const league = leagueById(params.leagueId);
    const { teamId, toDivision } = await request.json();
    if (!league?.stage || !runsLeague(request, league) || !league.teams.some((team) => team.id === teamId)) {
      return response(404).empty();
    }
    if (league.stageClosed) {
      return response(409).json(conflict("stage-closed", "This League's last Stage has closed: the League has ended."), problemJson);
    }
    const divisions = ladderDivisions(league);
    const from = divisions.find((division) => division.competitors.some((competitor) => competitor.teamId === teamId));
    if (!from) {
      return response(409).json(conflict("not-on-the-ladder", "This Team isn't on the ladder this Stage."), problemJson);
    }
    const to = Number(toDivision);
    if (to > divisions.length || to === from.number) {
      return response(400).json(invalid("toDivision"), problemJson);
    }
    const played = from.games.some((game) => game.score && (game.firstTeamId === teamId || game.secondTeamId === teamId));
    if (played) {
      return response(409).json(conflict("team-has-results", "This Team has a result this Stage, so it can't move now."), problemJson);
    }
    if (from.competitors.length <= 2) {
      return response(409).json(conflict("division-too-small", `Division ${from.number} would be left with one Team.`), problemJson);
    }
    // As the API deletes them: the Team's Games where it was, their not-played marks with them, so if it
    // comes back it gets new Games, not its old ones (#219 review).
    for (const game of from.games.filter((game) => game.firstTeamId === teamId || game.secondTeamId === teamId)) {
      voided.delete(game.id);
      for (const [key, id] of gameIds) if (id === game.id) gameIds.delete(key);
    }
    const order = divisions.map((division) => division.competitors.map((competitor) => competitor.teamId).filter((team) => team !== teamId));
    order[to - 1].push(teamId);
    league.ladder = order.flat();
    league.divisions = order.map((division) => division.length);
    return response(204).empty();
  }),

  // Closing the current Stage (#10): as the API's GetStageClose and CloseStage.
  http.get("/leagues/{leagueId}/close", ({ request, params, response }) => {
    const league = leagueById(params.leagueId);
    if (!league?.stage || !runsLeague(request, league)) {
      return response(404).empty();
    }
    if (league.stageClosed) {
      return response(409).json(conflict("stage-closed", "This League's last Stage has closed: the League has ended."), problemJson);
    }
    const divisions = ladderDivisions(league);
    const next = nextStage(league, league.stage.endDate);
    return response(200).json({
      stage: league.stage,
      leagueEndDate: league.endDate,
      heldUp: clubToday() > league.stage.endDate,
      divisions,
      awaiting: awaitingOf(divisions),
      waiting: next ? waitingTeams(league).map((teamId) => ({ teamId, players: teamPlayers(league, teamId) })) : [],
      nextStage: next,
    });
  }),

  http.post("/leagues/{leagueId}/close", async ({ request, params, response }) => {
    const league = leagueById(params.leagueId);
    if (!league?.stage || !runsLeague(request, league)) {
      return response(404).empty();
    }
    if (league.stageClosed) {
      return response(409).json(conflict("stage-closed", "This League's last Stage has closed: the League has ended."), problemJson);
    }
    const body = await request.json();
    const divisions = ladderDivisions(league);
    if (awaitingOf(divisions).unscoredGameIds.length > 0) {
      return response(409).json(conflict("stage-queue-not-empty", "Games with no score are holding the Stage up."), problemJson);
    }
    // The confirmed order must still follow each Division's Standings; ties in any order.
    const follows =
      body.divisions.length === divisions.length &&
      divisions.every((division, index) => {
        const order = body.divisions[index];
        const rank = new Map(division.standings.map((standing) => [standing.teamId, Number(standing.rank)]));
        return (
          order.length === rank.size &&
          new Set(order).size === order.length &&
          order.every((team, at) => rank.has(team) && (at === 0 || rank.get(team)! >= rank.get(order[at - 1])!))
        );
      });
    if (!follows) {
      return response(409).json(conflict("standings-changed", "The Standings have changed since the close screen loaded."), problemJson);
    }
    const next = nextStage(league, league.stage.endDate);
    const waiting = next ? waitingTeams(league) : [];
    if (new Set(body.joining).size !== body.joining.length || !body.joining.every((team) => waiting.includes(team))) {
      return response(409).json(conflict("league-changed", "The Teams waiting for a place have changed."), problemJson);
    }
    const errors: Record<string, string[]> = {};
    if (!next && body.nextStartDate) {
      errors.nextStartDate = ["This is the League's last Stage: closing it ends the League."];
    } else if (next && !body.nextStartDate) {
      errors.nextStartDate = ["Give the next Stage its dates."];
    } else if (body.nextStartDate && body.nextEndDate) {
      if (body.nextStartDate <= league.stage.endDate) {
        errors.nextStartDate = ["The next Stage starts after this one ends."];
      }
      if (body.nextEndDate > league.endDate) {
        errors.nextEndDate = ["The League ends before then."];
      }
    }
    if (Object.keys(errors).length > 0) {
      return response(400).json({ title: "Validation failed", status: 400, errors }, problemJson);
    }
    if (!body.nextStartDate || !body.nextEndDate) {
      league.stageClosed = true;
      return response(204).empty();
    }
    const placed = relegate(body.divisions, body.joining);
    league.pastStages.push(currentOf(league)!);
    league.stage = { number: league.stage.number + 1, startDate: body.nextStartDate, endDate: body.nextEndDate };
    league.ladder = placed.flat();
    league.divisions = placed.map((division) => division.length);
    return response(204).empty();
  }),


  // A Game for its Match screen (#38): seen by whoever can see its League's ladder.
  http.get("/games/{gameId}", ({ request, params, response }) => {
    const fixed = fixtureGames[params.gameId];
    if (fixed) {
      return response(200).json(fixed);
    }
    const game = gameById(params.gameId);
    const canManage = game ? runsLeague(request, game.league) : false;
    const mine = playersOf(request);
    if (!game || !(mine.some((player) => game.league.participants.some((p) => p.playerId === player.id)) || canManage)) {
      return response(404).empty();
    }
    const { league } = game;
    const score = scores.get(game.id);
    const sides = [game.firstTeamId, game.secondTeamId].map((teamId, index) => ({
      side: index + 1,
      teamId,
      players: teamPlayers(league, teamId),
    }));
    return response(200).json({
      id: game.id,
      leagueId: league.id,
      leagueName: league.name,
      clubName: MOCK_CLUB.name,
      type: league.type,
      scoreFormat: league.scoreFormat,
      // A mock Stage never closes: closing is #10.
      stage: game.stage,
      division: game.division,
      sides,
      score: score
        ? {
            playedOn: score.playedOn,
            sets: setScores(league, score.sets),
            winner: score.winner,
            retired: score.retired,
            submittedBy: score.submittedBy,
            recordedByManager: score.recordedByManager,
            submittedAt: score.submittedAt,
            version: score.version,
          }
        : null,
      notPlayed: voided.has(game.id),
      history: (score?.history ?? []).map((entry) => ({
        ...entry,
        before: entry.before ? snapshotOf(league, entry.before) : null,
        after: snapshotOf(league, entry.after),
      })),
      timeZone: "Europe/London",
      yourPlayerIds: mine
        .filter((player) => sides.some((side) => side.players.some((p) => p.playerId === player.id)))
        .map((player) => player.id),
      today: clubToday(),
      canManage,
      managers: managersOf(league).map(({ name }) => ({ name })),
    });
  }),

  // As the API's SubmitScore (#38): a Player in the Game enters it once, checked by the same rules.
  http.post("/games/{gameId}/score", async ({ request, params, response }) => {
    const { playerId, playedOn, sets, retirementWinner } = await request.json();
    const player = playersOf(request).find((candidate) => candidate.id === playerId);
    const game = gameById(params.gameId);
    if (!player || !game) {
      return response(404).empty();
    }
    const { league } = game;
    const inLeague = playersOf(request).some((mine) => league.participants.some((p) => p.playerId === mine.id));
    if (!(inLeague || runsLeague(request, league))) {
      return response(404).empty();
    }
    const team = league.participants.find((participant) => participant.playerId === player.id)?.teamId;
    if (team !== game.firstTeamId && team !== game.secondTeamId) {
      return response(403).json({ type: "not-in-game", title: "Only a Player in this game can enter its score.", status: 403 }, problemJson);
    }
    if (scores.has(game.id)) {
      return response(409).json(conflict("already-scored", "This game already has a score."), problemJson);
    }
    if (game.stage.closed) {
      return response(409).json(conflict("stage-closed", "This game's Stage has closed, so only a Manager can change its score."), problemJson);
    }
    if (voided.has(game.id)) {
      return response(409).json(conflict("not-played", "A manager has marked this game as not played, so it takes no score."), problemJson);
    }
    const { errors, after } = checkEntry(league, game.stage, { playedOn, sets, retirementWinner });
    if (!after) {
      return response(400).json({ title: "Validation failed", status: 400, errors }, problemJson);
    }
    const submittedAt = new Date().toISOString();
    scores.set(game.id, {
      ...after,
      submittedBy: player.name,
      recordedByManager: false,
      submittedAt,
      version: (scoreVersions += 1),
      history: [{ kind: "Entered", by: player.name, at: submittedAt, before: null, after }],
    });
    return response(204).empty();
  }),

  // As the API's RecordScore (#10): a Manager, for a Game nobody scored.
  http.post("/games/{gameId}/score/record", async ({ request, params, response }) => {
    const body = await request.json();
    const game = gameById(params.gameId);
    if (!game) {
      return response(404).empty();
    }
    if (!runsLeague(request, game.league)) {
      return response(403).json({ title: "Forbidden", status: 403 }, problemJson);
    }
    if (scores.has(game.id)) {
      return response(409).json(conflict("already-scored", "This game already has a score."), problemJson);
    }
    if (game.stage.closed) {
      return response(409).json(conflict("stage-closed", "This game's Stage has closed, so only a Manager can change its score."), problemJson);
    }
    if (voided.has(game.id)) {
      return response(409).json(conflict("not-played", "A manager has marked this game as not played, so it takes no score."), problemJson);
    }
    const { errors, after } = checkEntry(game.league, game.stage, body);
    if (!after) {
      return response(400).json({ title: "Validation failed", status: 400, errors }, problemJson);
    }
    const by = playersOf(request)[0]?.name ?? null;
    const submittedAt = new Date().toISOString();
    scores.set(game.id, {
      ...after,
      submittedBy: by,
      recordedByManager: true,
      submittedAt,
      version: (scoreVersions += 1),
      history: [{ kind: "Recorded", by, at: submittedAt, before: null, after }],
    });
    return response(204).empty();
  }),

  // As the API's VoidGame and UnvoidGame (#10): Managers only; a scored Game was played.
  http.put("/games/{gameId}/not-played", ({ request, params, response }) => {
    const game = gameById(params.gameId);
    if (!game) {
      return response(404).empty();
    }
    if (!runsLeague(request, game.league)) {
      return response(403).json({ title: "Forbidden", status: 403 }, problemJson);
    }
    if (scores.has(game.id)) {
      return response(409).json(conflict("already-scored", "This game already has a score."), problemJson);
    }
    if (game.stage.closed) {
      return response(409).json(conflict("stage-closed", "This game's Stage has closed, so only a Manager can change its score."), problemJson);
    }
    voided.add(game.id);
    return response(204).empty();
  }),

  http.delete("/games/{gameId}/not-played", ({ request, params, response }) => {
    const game = gameById(params.gameId);
    if (!game) {
      return response(404).empty();
    }
    if (!runsLeague(request, game.league)) {
      return response(403).json({ title: "Forbidden", status: 403 }, problemJson);
    }
    if (game.stage.closed) {
      return response(409).json(conflict("stage-closed", "This game's Stage has closed, so only a Manager can change its score."), problemJson);
    }
    voided.delete(game.id);
    return response(204).empty();
  }),

  // As the API's CorrectScore (#38): a Player in the Game, while the Stage is open (and a mock one always is).
  http.put("/games/{gameId}/score", async ({ request, params, response }) => {
    const body = await request.json();
    const player = playersOf(request).find((candidate) => candidate.id === body.playerId);
    const game = gameById(params.gameId);
    if (!player || !game) {
      return response(404).empty();
    }
    const { league } = game;
    const inLeague = playersOf(request).some((mine) => league.participants.some((p) => p.playerId === mine.id));
    if (!(inLeague || runsLeague(request, league))) {
      return response(404).empty();
    }
    const team = league.participants.find((participant) => participant.playerId === player.id)?.teamId;
    if (team !== game.firstTeamId && team !== game.secondTeamId) {
      return response(403).json({ type: "not-in-game", title: "Only a Player in this game can enter its score.", status: 403 }, problemJson);
    }
    const score = scores.get(game.id);
    if (!score) {
      return response(409).json(conflict("not-scored", "This game has no score to change yet."), problemJson);
    }
    if (game.stage.closed) {
      return response(409).json(conflict("stage-closed", "This game's Stage has closed, so only a Manager can change its score."), problemJson);
    }
    const { errors, after } = checkEntry(league, game.stage, body);
    if (!after) {
      return response(400).json({ title: "Validation failed", status: 400, errors }, problemJson);
    }
    if (!changeScore(score, Number(body.version), after, "Corrected", player.name)) {
      return response(409).json(conflict("score-changed", "The score changed after you opened it."), problemJson);
    }
    return response(204).empty();
  }),

  // As the API's OverrideScore (#38): Managers only, open Stage or closed.
  http.put("/games/{gameId}/score/override", async ({ request, params, response }) => {
    const body = await request.json();
    const game = gameById(params.gameId);
    if (!game) {
      return response(404).empty();
    }
    if (!runsLeague(request, game.league)) {
      return response(403).json({ title: "Forbidden", status: 403 }, problemJson);
    }
    const score = scores.get(game.id);
    if (!score) {
      return response(409).json(conflict("not-scored", "This game has no score to change yet."), problemJson);
    }
    const { errors, after } = checkEntry(game.league, game.stage, body);
    if (!after) {
      return response(400).json({ title: "Validation failed", status: 400, errors }, problemJson);
    }
    if (!changeScore(score, Number(body.version), after, "Overridden", playersOf(request)[0]?.name ?? null)) {
      return response(409).json(conflict("score-changed", "The score changed after you opened it."), problemJson);
    }
    return response(204).empty();
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

  // As the API's GetPlayerGames (#42): Home's Games this Stage, by League, the soonest deadline first.
  http.get("/players/{playerId}/games", ({ request, params, response }) => {
    const player = playersOf(request).find((candidate) => candidate.id === params.playerId);
    if (!player) {
      return response(404).empty();
    }
    const list = leagues
      .filter((league) => league.stage && !league.stageClosed && isClubMember(player, league.clubId))
      .filter((league) => league.participants.some((participant) => participant.playerId === player.id))
      .sort((a, b) => a.stage!.endDate.localeCompare(b.stage!.endDate) || a.name.localeCompare(b.name))
      .map((league) => {
        const divisions = ladderDivisions(league);
        const awaiting = awaitingOf(divisions);
        const teamId = league.participants.find((participant) => participant.playerId === player.id)?.teamId;
        const division = divisions.find((candidate) => candidate.competitors.some((competitor) => competitor.teamId === teamId));
        const players = (team: string) => division!.competitors.find((competitor) => competitor.teamId === team)!.players;
        return {
          leagueId: league.id,
          leagueName: league.name,
          clubName: MOCK_CLUB.name,
          stage: league.stage!,
          division: division?.number ?? null,
          heldUp: clubToday() > league.stage!.endDate,
          unscoredGames: awaiting.unscoredGameIds.length,
          openTies: awaiting.ties.length,
          games: (division?.games ?? [])
            .filter((game) => game.firstTeamId === teamId || game.secondTeamId === teamId)
            .map((game) => {
              const yourSide = game.firstTeamId === teamId ? 1 : 2;
              const [ours, theirs] = yourSide === 1 ? [game.firstTeamId, game.secondTeamId] : [game.secondTeamId, game.firstTeamId];
              return {
                gameId: game.id,
                yourSide,
                partners: players(ours).filter((candidate) => candidate.playerId !== player.id).map((candidate) => candidate.name),
                opponents: players(theirs).map((candidate) => candidate.name),
                score: game.score,
                notPlayed: game.notPlayed,
              };
            }),
        };
      });
    return response(200).json({ leagues: list, page: wholeList(list.length) });
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

  // In place of an email, as the real API's Log provider does: whoever runs
  // the mock opens the link from its log. The token is the address itself.
  http.post("/auth/magic-link", async ({ request, response }) => {
    const { email, join } = await request.json();
    const link = new URL("/auth/verify", process.env.PUBLIC_BASE_URL ?? "http://localhost:3000");
    link.searchParams.set("token", email);
    if (isJoinCode(join)) link.searchParams.set("join", join);
    console.info(`[mock-api] Sign-in link: ${link.href}`);
    return response(202).empty();
  }),

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
    // An Admin manages every Club, as with the real API.
    return response(200).json({
      userId: user.id,
      email,
      admin: user.admin,
      manages: user.admin || user.clubs.size > 0 || user.leagues.size > 0,
      managesClub: user.admin || [...user.clubs.values()].includes("Manager"),
    });
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
    const clubId = query.get("clubId");
    // In this Club (#245): a Player there or asking (only the shared Club takes members), a role, or a League of its.
    const inClub = (email: string, user: MockUser) =>
      !clubId ||
      user.clubs.has(clubId) ||
      leagues.some((league) => league.clubId === clubId && user.leagues.has(league.id)) ||
      (clubId === MOCK_CLUB.id && allPlayers().some((player) => player.email === email && player.membership));
    const matching = [...usersByEmail]
      .reverse()
      .filter(
        ([email, user]) =>
          inClub(email, user) &&
          (!search ||
            email.includes(search) ||
            names(email).some((player) => player.name.toLowerCase().includes(search))),
      )
      .sort(([a], [b]) => Number(b === search) - Number(a === search));
    const users = matching.slice(offset, offset + limit).map(([email, user]) => ({
      id: user.id,
      email,
      clubs: [...user.clubs].map(([clubId, role]) => ({ clubId, clubName: clubNameOf(clubId), role })),
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
    user.admin = true;
    return response(204).empty();
  }),

  http.delete("/users/{userId}/roles/{role}", ({ params, response }) => {
    const user = userById(params.userId);
    if (!user) {
      return response(404).empty();
    }
    if (usersByEmail.get(CONFIGURED_ADMIN) === user) {
      return response(409).json({ title: "The Admin role can't be removed from this User.", status: 409 }, problemJson);
    }
    user.admin = false;
    return response(204).empty();
  }),

  // Who manages a Club (#110).
  http.put("/clubs/{clubId}/managers/{userId}", ({ params, response }) => {
    const user = userById(params.userId);
    if (!user) {
      return response(404).empty();
    }
    // Whatever role they had there: a League manager becomes a manager.
    user.clubs.set(params.clubId, "Manager");
    return response(204).empty();
  }),

  http.delete("/clubs/{clubId}/managers/{userId}", ({ params, response }) => {
    const user = userById(params.userId);
    if (!user) {
      return response(404).empty();
    }
    if (user.clubs.get(params.clubId) === "Manager") user.clubs.delete(params.clubId);
    return response(204).empty();
  }),

  // Who may create Leagues in a Club (#220): its managers give and take the role.
  http.put("/clubs/{clubId}/league-managers/{userId}", ({ request, params, response }) => {
    const email = emailOfUser(params.userId);
    if (!runsClub(request, params.clubId) || !email) {
      return response(404).empty();
    }
    if (!playsInTheClub(email)) {
      return response(409).json(notInClub, problemJson);
    }
    const user = userOf(email);
    if (!user.clubs.has(params.clubId)) user.clubs.set(params.clubId, "LeagueManager");
    return response(204).empty();
  }),

  http.delete("/clubs/{clubId}/league-managers/{userId}", ({ request, params, response }) => {
    const user = userById(params.userId);
    if (!runsClub(request, params.clubId) || !user) {
      return response(404).empty();
    }
    if (user.clubs.get(params.clubId) === "LeagueManager") user.clubs.delete(params.clubId);
    return response(204).empty();
  }),

  // Deleting a User (#116), as the API: their Players become Former players and leave the Club, and their
  // sessions end. Refused for one's own account and the configured Admin.
  http.delete("/users/{userId}", ({ request, params, response }) => {
    const entry = [...usersByEmail].find(([, user]) => user.id === params.userId);
    if (!entry) {
      return response(404).empty();
    }
    const [email] = entry;
    if (email === emailOf(request) || email === CONFIGURED_ADMIN) {
      const type = email === CONFIGURED_ADMIN ? "admin-by-setting" : "own-account";
      return response(409).json({ type, title: "This User can't be deleted.", status: 409 }, problemJson);
    }
    for (const player of allPlayers().filter((candidate) => candidate.email === email)) {
      Object.assign(player, { name: "Former player", gender: null, level: null, email: "" });
      delete player.membership;
    }
    usersByEmail.delete(email);
    userIds.delete(email);
    invitedEmails.delete(email);
    deletedAtSession.set(email, sessions);
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

  // Not in the OpenAPI contract: the real API's liveness check, which web's /health asks.
  http.untyped.get("/health", () => HttpResponse.text("Healthy")),
];
