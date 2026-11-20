import { http as untypedHttp, passthrough } from "msw";
import { createOpenApiHttp } from "openapi-msw";
import type { components, paths } from "@/lib/api/schema";

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
      id: `00000000-0000-4000-8000-2${String(usersByEmail.size + 1).padStart(11, "0")}`,
      manager: local.includes("manager"),
      admin: local.includes("admin"),
    };
    usersByEmail.set(email, user);
  }
  return user;
}

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
  participants: { playerId: string; joinedAt: string }[];
  requests: { playerId: string; status: Schemas["LeagueJoinRequestStatus"]; requestedAt: string; decidedAt: string | null }[];
}

/** The page block of a list that isn't paged: every match, from the start (ADR-0012). */
const wholeList = (total: number) => ({ total, offset: 0, limit: null });

const leagues: MockLeague[] = [];
let leaguesMade = 0;

const leagueById = (id: string) => leagues.find((league) => league.id === id);
const isClubMember = (player: MockPlayer | undefined, clubId: string) =>
  clubId === MOCK_CLUB.id && player?.membership?.approved === true;

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
});

const conflict = (type: string, title: string) => ({ type, title, status: 409 });

const problemJson = { headers: { "Content-Type": "application/problem+json" } };
const invalid = (field: string) => ({ title: "Validation failed", status: 400, errors: { [field]: ["Invalid."] } });

export const handlers = [
  http.get("/clubs", ({ request, response }) => {
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
    const session = sessionOf(request) ?? "";
    const list = leagues
      .filter((league) => league.clubId === params.clubId && league.session === session)
      .sort((a, b) => b.startDate.localeCompare(a.startDate) || a.name.localeCompare(b.name))
      .map((league) => ({
        ...leagueFacts(league),
        participantCount: league.participants.length,
        pendingRequestCount: league.requests.filter((joinRequest) => joinRequest.status === "Pending").length,
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
      scoreFormat: {
        gamesPerSet: Number(body.scoreFormat.gamesPerSet),
        setsPerGame: Number(body.scoreFormat.setsPerGame),
        matchTiebreakDecider: body.scoreFormat.matchTiebreakDecider,
      },
      participants: [],
      requests: [],
    };
    leagues.push(league);
    return response(201).json({ id: league.id, name: league.name });
  }),

  http.get("/leagues/{leagueId}", ({ request, params, response }) => {
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
      participants: league.participants.map(({ playerId, joinedAt }) => ({
        playerId,
        name: player(playerId).name,
        email: player(playerId).email,
        rating: player(playerId).rating,
        joinedAt,
      })),
      joinRequests: pendingFirst.map((joinRequest) => ({
        ...joinRequest,
        name: player(joinRequest.playerId).name,
        email: player(joinRequest.playerId).email,
        rating: player(joinRequest.playerId).rating,
      })),
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
    const now = new Date().toISOString();
    const pending = league.requests.find((joinRequest) => joinRequest.playerId === player.id && joinRequest.status === "Pending");
    if (pending) {
      pending.status = "Approved";
      pending.decidedAt = now;
    }
    league.participants.push({ playerId: player.id, joinedAt: pending?.requestedAt ?? now });
    return response(204).empty();
  }),

  // Removing before the start (#159): an approved request goes too, so they may ask again.
  http.delete("/leagues/{leagueId}/participants/{playerId}", ({ params, response }) => {
    const league = leagueById(params.leagueId);
    if (!league) {
      return response(404).empty();
    }
    const before = league.participants.length;
    league.participants = league.participants.filter((participant) => participant.playerId !== params.playerId);
    if (league.participants.length < before) {
      league.requests = league.requests.filter(
        (joinRequest) => !(joinRequest.playerId === params.playerId && joinRequest.status === "Approved"),
      );
    }
    return response(204).empty();
  }),

  http.post("/leagues/{leagueId}/join-requests", async ({ request, params, response }) => {
    const { playerId } = await request.json();
    const player = playersOf(request).find((candidate) => candidate.id === playerId);
    const league = leagueById(params.leagueId);
    if (!player || !league || !isClubMember(player, league.clubId)) {
      return response(404).empty();
    }
    if (league.participants.some((participant) => participant.playerId === player.id)) {
      return response(409).json(conflict("already-in-league", "This Player is already in the League."), problemJson);
    }
    if (league.requests.some((joinRequest) => joinRequest.playerId === player.id)) {
      return response(409).json(conflict("already-asked", "This Player has already asked to join the League."), problemJson);
    }
    league.requests.push({ playerId: player.id, status: "Pending", requestedAt: new Date().toISOString(), decidedAt: null });
    return response(204).empty();
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
    if (joinRequest.status === "Pending") {
      joinRequest.status = "Approved";
      joinRequest.decidedAt = new Date().toISOString();
      league.participants.push({ playerId: joinRequest.playerId, joinedAt: joinRequest.requestedAt });
    }
    return response(204).empty();
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
        return {
          ...leagueFacts(league),
          clubId: league.clubId,
          clubName: MOCK_CLUB.name,
          participantCount: league.participants.length,
          joined: league.participants.some((participant) => participant.playerId === player.id),
          joinRequest: joinRequest
            ? { status: joinRequest.status, requestedAt: joinRequest.requestedAt, decidedAt: joinRequest.decidedAt }
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
  http.get("/users", ({ query, response }) => {
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
