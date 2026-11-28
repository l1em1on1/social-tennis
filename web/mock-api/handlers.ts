import type { components, paths } from "@/lib/api/schema";
import { isJoinCode } from "@/lib/join";
import { sidesOf } from "@/lib/league";
import { HttpResponse } from "msw";
import { createOpenApiHttp } from "openapi-msw";
import {
  CLUBS,
  FIXTURE_ADMIN,
  FIXTURE_JOIN_CODE,
  FIXTURE_MANAGER,
  FIXTURE_TODAY,
  FIXTURE_USER_IDS,
  LEAGUE_RUNNERS,
  LEAGUES_BY_START,
  STAGE_CLOSED,
  TIME_ZONE,
  fixtureCloses,
  fixtureGameScores,
  fixtureLadders,
  fixtureLeaguePages,
  fixtureLeagueStarts,
  fixtureMembers,
  fixturePlayersOf,
  fixtureUsers,
  fixtureDateSetBy,
  fixtureFreeDays,
  fixtureGameAvailability,
} from "./fixture";

/**
 * The mock API's answers (ADR-0009, ADR-0033 as amended by #234): reads of the
 * fixed world in fixture.ts, as the real API would answer the User signed in,
 * typed against the committed OpenAPI schema, so the mock can't drift from the
 * contract silently. It changes nothing: server.ts refuses every write but
 * signing in and out. Behaviour is tested against the real API; this is for
 * the snapshots, and for looking at the app without it.
 *
 * Mock sign-in has no inbox: the link goes to this server's log, any token
 * redeems, and the session is the token. A token that is an email address
 * signs in as that User: one of fixture.ts's personas, or anyone else, who
 * holds nothing.
 */
const http = createOpenApiHttp<paths>();

type Schemas = components["schemas"];

// Session tokens must survive a cookie round trip unencoded, like the real API's base64url ones.
const SESSION_PREFIX = "mock-session.";
let sessions = 0;

function emailOf(request: Request): string | undefined {
  const bearer = request.headers.get("Authorization")?.replace(/^Bearer /, "");
  if (!bearer?.startsWith(SESSION_PREFIX)) return undefined;
  const remembered = Buffer.from(bearer.slice(SESSION_PREFIX.length).split(".")[0], "base64url").toString();
  return remembered.includes("@") ? remembered.toLowerCase() : "player@example.com";
}

const problemJson = { headers: { "Content-Type": "application/problem+json" } };
const forbidden = { title: "Forbidden", status: 403 };
const wholeList = (total: number) => ({ total, offset: 0, limit: null });

/** What a User may do, as the API's roles say it (ADR-0022, ADR-0034, #220). */
function rolesOf(email: string) {
  const admin = email === FIXTURE_ADMIN;
  const clubRoles = fixtureMembers[CLUBS[0].id].roles.filter((role) => role.email === email);
  const manages = (clubId: string) =>
    admin || (fixtureMembers[clubId]?.roles ?? []).some((role) => role.email === email && role.role === "Manager");
  const leagueManagerIn = (clubId: string) =>
    (fixtureMembers[clubId]?.roles ?? []).some((role) => role.email === email && role.role === "LeagueManager");
  const runs = (league: Schemas["GetLeagueResponse"]) => manages(league.clubId) || (LEAGUE_RUNNERS[league.id] ?? []).includes(email);
  return {
    admin,
    manages,
    leagueManagerIn,
    runs,
    managesAny: admin || email === FIXTURE_MANAGER || clubRoles.length > 0,
    managesClub: admin || CLUBS.some((club) => manages(club.id)),
  };
}

const playersOf = (email: string) => fixturePlayersOf(email);
const playerIdsOf = (email: string) => new Set(playersOf(email).map((player) => player.id));
const isMember = (playerId: string, clubId: string) =>
  (fixtureMembers[clubId]?.members ?? []).some((m) => m.playerId === playerId && m.approved);

const leagueFacts = (league: Schemas["GetLeagueResponse"]) => ({
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
  standingsMode: league.standingsMode,
});

const teamOf = (league: Schemas["GetLeagueResponse"], playerId: string) =>
  league.participants.find((participant) => participant.playerId === playerId)?.teamId ?? null;

/** A Game, wherever it is: its League, Division, ladder entry and Players. */
function gameById(gameId: string) {
  for (const [leagueId, ladder] of Object.entries(fixtureLadders)) {
    for (const division of ladder.divisions) {
      const game = division.games.find((candidate) => candidate.id === gameId);
      if (game) return { league: fixtureLeaguePages[leagueId], ladder, division, game };
    }
  }
  return undefined;
}

const namesOf = (division: Schemas["LadderDivision"], teamId: string) =>
  division.competitors.find((competitor) => competitor.teamId === teamId)?.players ?? [];

/** A Side's Players: one Team's, or in Foursomes a pair of Teams of one (#14, ADR-0036). */
const sidePlayers = (division: Schemas["LadderDivision"], teams: string[]) => teams.flatMap((teamId) => namesOf(division, teamId));

/** As the Match screen gets them (#21): each Player, and whether their Team has withdrawn. */
const gamePlayers = (division: Schemas["LadderDivision"], teams: string[]) =>
  teams.flatMap((teamId) => {
    const withdrawn = division.competitors.some((c) => c.teamId === teamId && c.withdrawn);
    return namesOf(division, teamId).map((player) => ({ ...player, withdrawn }));
  });

export const handlers = [
  // In place of an email, as the real API's Log provider does: whoever runs the mock opens the link from its log.
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

  http.delete("/auth/sessions/current", ({ response }) => response(204).empty()),

  http.get("/auth/me", ({ request, response }) => {
    const email = emailOf(request);
    if (!email) {
      return response(401).json({ title: "Unauthorized", status: 401 }, problemJson);
    }
    const roles = rolesOf(email);
    return response(200).json({
      userId: FIXTURE_USER_IDS[email] ?? "00000000-0000-4000-8000-0000000000ff",
      email,
      admin: roles.admin,
      manages: roles.managesAny,
      managesClub: roles.managesClub,
    });
  }),

  http.get("/players", ({ request, response }) => {
    const players = playersOf(emailOf(request) ?? "");
    return response(200).json({ players, page: wholeList(players.length) });
  }),

  // The Player's Leagues with an open Stage (#42): their Division, their Games, and what holds the Stage up.
  http.get("/players/{playerId}/games", ({ request, params, response }) => {
    if (!playerIdsOf(emailOf(request) ?? "").has(params.playerId)) {
      return response(404).empty();
    }
    const leagues = Object.entries(fixtureLadders)
      .filter(([leagueId, ladder]) => ladder.stage && !STAGE_CLOSED[leagueId])
      .flatMap(([leagueId, ladder]) => {
        const league = fixtureLeaguePages[leagueId];
        const teamId = teamOf(league, params.playerId);
        const division = ladder.divisions.find((candidate) => candidate.competitors.some((c) => c.teamId === teamId));
        if (!division || !teamId) return [];
        return [
          {
            leagueId,
            leagueName: ladder.name,
            clubName: ladder.clubName,
            stage: ladder.stage!,
            division: division.number,
            heldUp: ladder.heldUp,
            unscoredGames: ladder.awaiting.unscoredGameIds.length,
            openTies: ladder.awaiting.ties.length,
            games: division.games
              .filter((game) => sidesOf(game).flat().includes(teamId))
              .map((game) => {
                const [first, second] = sidesOf(game);
                const yourSide = first.includes(teamId) ? 1 : 2;
                const [ours, theirs] = yourSide === 1 ? [first, second] : [second, first];
                return {
                  gameId: game.id,
                  yourSide,
                  partners: sidePlayers(division, ours).filter((p) => p.playerId !== params.playerId).map((p) => p.name),
                  opponents: sidePlayers(division, theirs).map((p) => p.name),
                  score: game.score,
                  notPlayed: game.notPlayed,
                  date: game.date ?? null,
                  startTime: game.startTime ?? null,
                  court: game.court ?? null,
                };
              }),
          },
        ];
      })
      .sort((a, b) => a.stage.endDate.localeCompare(b.stage.endDate) || a.leagueName.localeCompare(b.leagueName));
    return response(200).json({ leagues, page: wholeList(leagues.length) });
  }),

  // A Player's own days in a League's current Stage (#9, #276), each with what counts on it: the fixture's free days
  // are their own, free any time, and every other day isn't free (no usual week). Saving them is a write: 501.
  http.get("/leagues/{leagueId}/players/{playerId}/availability", ({ request, params, response }) => {
    const league = fixtureLeaguePages[params.leagueId];
    const ladder = fixtureLadders[params.leagueId];
    if (
      !playerIdsOf(emailOf(request) ?? "").has(params.playerId) ||
      !league?.participants.some((p) => p.playerId === params.playerId) ||
      !ladder?.stage ||
      STAGE_CLOSED[params.leagueId]
    ) {
      return response(404).empty();
    }
    const free = new Set(fixtureFreeDays[params.leagueId]?.[params.playerId] ?? []);
    const days = [];
    for (let day = Date.parse(`${ladder.stage.startDate}T00:00:00Z`); day <= Date.parse(`${ladder.stage.endDate}T00:00:00Z`); day += 86_400_000) {
      const date = new Date(day).toISOString().slice(0, 10);
      days.push({ date, own: free.has(date), free: free.has(date), from: null, to: null });
    }
    return response(200).json({ stage: ladder.stage, days, lastDay: ladder.stage.endDate });
  }),

  // The Leagues of the Player's Clubs, and the ones they're in (#5, #7).
  http.get("/players/{playerId}/leagues", ({ request, params, response }) => {
    if (!playerIdsOf(emailOf(request) ?? "").has(params.playerId)) {
      return response(404).empty();
    }
    const playerId = params.playerId;
    const leagues = LEAGUES_BY_START.filter(
      (league) => isMember(playerId, league.clubId) || league.participants.some((p) => p.playerId === playerId),
    ).map((league) => {
      const joinRequest = league.joinRequests.find((candidate) => candidate.playerId === playerId);
      const namedIn = league.joinRequests.find((candidate) => candidate.partner?.playerId === playerId && candidate.status === "Pending");
      const teamId = teamOf(league, playerId);
      const partner = teamId
        ? league.participants.find((p) => p.teamId === teamId && p.playerId !== playerId)
        : undefined;
      const ladder = fixtureLadders[league.id];
      const division = ladder?.divisions.find((candidate) => candidate.competitors.some((c) => c.teamId === teamId));
      return {
        ...leagueFacts(league),
        clubId: league.clubId,
        clubName: league.clubName,
        participantCount: league.participants.length,
        joined: league.participants.some((p) => p.playerId === playerId),
        joinRequest: joinRequest
          ? { status: joinRequest.status, requestedAt: joinRequest.requestedAt, decidedAt: joinRequest.decidedAt, partnerName: joinRequest.partner?.name ?? null }
          : null,
        partnerName: partner?.name ?? null,
        stage: league.stage,
        place:
          division && teamId
            ? {
                division: division.number,
                opponents: division.competitors
                  .filter((c) => c.teamId !== teamId)
                  .map((c) => ({ teamId: c.teamId, names: c.players.map((p) => p.name) })),
              }
            : null,
        partnerRequest: namedIn ? { playerId: namedIn.playerId, name: namedIn.name, requestedAt: namedIn.requestedAt } : null,
        // As its ladder says (#21): an ended League has nothing left to leave.
        ended: ladder?.ended ?? false,
      };
    });
    return response(200).json({ leagues, page: wholeList(leagues.length) });
  }),

  // Whom the Player could name as partner in a doubles League (#6): its Club's members not in a Team or a waiting request.
  http.get("/leagues/{leagueId}/partners", ({ request, params, response }) => {
    const playerId = new URL(request.url).searchParams.get("playerId") ?? "";
    const league = fixtureLeaguePages[params.leagueId];
    if (!league || !playerIdsOf(emailOf(request) ?? "").has(playerId) || !isMember(playerId, league.clubId)) {
      return response(404).empty();
    }
    const busy = new Set([
      ...league.participants.filter((p) => p.teamId).map((p) => p.playerId),
      ...league.joinRequests.filter((r) => r.status === "Pending").flatMap((r) => [r.playerId, r.partner?.playerId ?? ""]),
    ]);
    const partners = fixtureMembers[league.clubId].members
      .filter((m) => m.approved && m.playerId !== playerId && !busy.has(m.playerId))
      .map(({ playerId: id, name }) => ({ playerId: id, name }))
      .sort((a, b) => a.name.localeCompare(b.name));
    return response(200).json({ partners, page: wholeList(partners.length) });
  }),

  // The Club a join link is for (#222); a link that's off, reset or unknown is a 404 alike.
  http.get("/join-links/{code}", ({ params, response }) =>
    params.code === FIXTURE_JOIN_CODE
      ? response(200).json({ clubId: CLUBS[0].id, clubName: CLUBS[0].name })
      : response(404).empty(),
  ),

  // The Clubs someone has a standing in (#110, #220): every one for an Admin.
  http.get("/clubs", ({ request, response }) => {
    const roles = rolesOf(emailOf(request) ?? "");
    const clubs = CLUBS.filter((club) => roles.manages(club.id) || roles.leagueManagerIn(club.id)).map((club) => ({
      ...club,
      canManage: roles.manages(club.id),
      canCreateLeagues: roles.manages(club.id) || roles.leagueManagerIn(club.id),
    }));
    return response(200).json({ clubs, page: wholeList(clubs.length) });
  }),

  http.get("/clubs/{clubId}/members", ({ request, params, response }) => {
    const members = fixtureMembers[params.clubId];
    if (!members) return response(404).empty();
    return rolesOf(emailOf(request) ?? "").manages(params.clubId)
      ? response(200).json(members)
      : response(403).json(forbidden, problemJson);
  }),

  // Riverside's link is on, Hillside's off, as the snapshots show.
  http.get("/clubs/{clubId}/join-link", ({ request, params, response }) =>
    rolesOf(emailOf(request) ?? "").manages(params.clubId)
      ? response(200).json({ code: params.clubId === CLUBS[0].id ? FIXTURE_JOIN_CODE : null })
      : response(403).json(forbidden, problemJson),
  ),

  // A Club's Leagues: all of them for its managers, the ones they run for a League manager (#220).
  http.get("/clubs/{clubId}/leagues", ({ request, params, response }) => {
    const roles = rolesOf(emailOf(request) ?? "");
    if (!roles.manages(params.clubId) && !roles.leagueManagerIn(params.clubId)) {
      return response(403).json(forbidden, problemJson);
    }
    const leagues = LEAGUES_BY_START.filter((league) => league.clubId === params.clubId && roles.runs(league)).map((league) => ({
      ...leagueFacts(league),
      participantCount: league.participants.length,
      pendingRequestCount: league.joinRequests.filter((r) => r.status === "Pending").length,
      stage: league.stage,
    }));
    return response(200).json({ leagues, page: wholeList(leagues.length) });
  }),

  // A League as whoever runs it sees it (#5, #220): emails of who else runs it only for its Club's managers.
  http.get("/leagues/{leagueId}", ({ request, params, response }) => {
    const league = fixtureLeaguePages[params.leagueId];
    if (!league) return response(404).empty();
    const roles = rolesOf(emailOf(request) ?? "");
    if (!roles.runs(league)) return response(403).json(forbidden, problemJson);
    const clubManager = roles.manages(league.clubId);
    return response(200).json({
      ...league,
      managers: league.managers.map((manager) => ({ ...manager, email: clubManager ? manager.email : null })),
      canAssignManagers: clubManager,
    });
  }),

  // Who could be added (#220): the Club's members who aren't in it, with emails only for its Club's managers.
  http.get("/leagues/{leagueId}/addable-players", ({ request, params, response }) => {
    const league = fixtureLeaguePages[params.leagueId];
    const roles = rolesOf(emailOf(request) ?? "");
    if (!league || !roles.runs(league)) return response(404).empty();
    const inLeague = new Set(league.participants.map((p) => p.playerId));
    const players = fixtureMembers[league.clubId].members
      .filter((m) => m.approved && !inLeague.has(m.playerId))
      .map(({ playerId, name, email }) => ({ playerId, name, email: roles.manages(league.clubId) ? email : null }))
      .sort((a, b) => a.name.localeCompare(b.name));
    return response(200).json({ players, page: wholeList(players.length) });
  }),

  http.get("/leagues/{leagueId}/start", ({ request, params, response }) => {
    const league = fixtureLeaguePages[params.leagueId];
    if (!league || !rolesOf(emailOf(request) ?? "").runs(league)) return response(404).empty();
    const start = fixtureLeagueStarts[params.leagueId];
    return start
      ? response(200).json(start)
      : response(409).json({ type: "already-started", title: "This League has started.", status: 409 }, problemJson);
  }),

  // The ladder (#7, #10), for its Players and whoever runs it.
  http.get("/leagues/{leagueId}/ladder", ({ request, params, response }) => {
    const league = fixtureLeaguePages[params.leagueId];
    if (!league) return response(404).empty();
    const email = emailOf(request) ?? "";
    const mine = playerIdsOf(email);
    const yourPlayerIds = league.participants.filter((p) => mine.has(p.playerId)).map((p) => p.playerId);
    const canManage = rolesOf(email).runs(league);
    if (yourPlayerIds.length === 0 && !canManage) return response(404).empty();
    const ladder = fixtureLadders[params.leagueId];
    if (!ladder) {
      return response(200).json({
        id: league.id,
        name: league.name,
        clubName: league.clubName,
        type: league.type,
        yourPlayerIds,
        managers: league.managers.map(({ name }) => ({ name })),
        canManage,
        stage: null,
        divisions: [],
        heldUp: false,
        awaiting: { unscoredGameIds: [], ties: [] },
        ended: false,
      });
    }
    return response(200).json({ ...ladder, yourPlayerIds, canManage });
  }),

  // Closing a Stage (#10): what it would do, for whoever runs the League.
  http.get("/leagues/{leagueId}/close", ({ request, params, response }) => {
    const league = fixtureLeaguePages[params.leagueId];
    if (!league?.stage || !rolesOf(emailOf(request) ?? "").runs(league)) return response(404).empty();
    const close = fixtureCloses[params.leagueId];
    return close
      ? response(200).json(close)
      : response(409).json({ type: "stage-closed", title: "This Stage has closed.", status: 409 }, problemJson);
  }),

  // A Game (#38), for anyone with a Player in its League and whoever runs it, as the API's GetGame.
  http.get("/games/{gameId}", ({ request, params, response }) => {
    const found = gameById(params.gameId);
    if (!found) return response(404).empty();
    const { league, ladder, division, game } = found;
    const email = emailOf(request) ?? "";
    const mine = playerIdsOf(email);
    const sides = sidesOf(game).map((teams, index) => ({
      side: index + 1,
      teamId: teams[0],
      players: gamePlayers(division, teams),
      partnerTeamId: teams[1] ?? null,
      // As the API says it (#21): a side whose Team, or in Foursomes either Player's, has withdrawn.
      withdrawn: teams.some((teamId) => division.competitors.some((c) => c.teamId === teamId && c.withdrawn)),
    }));
    const yourPlayerIds = sides.flatMap((side) => side.players).filter((p) => mine.has(p.playerId)).map((p) => p.playerId);
    const canManage = rolesOf(email).runs(league);
    const inLeague = league.participants.some((p) => mine.has(p.playerId));
    if (!inLeague && !canManage) return response(404).empty();
    const recorded = fixtureGameScores[game.id];
    return response(200).json({
      id: game.id,
      leagueId: league.id,
      leagueName: league.name,
      clubName: league.clubName,
      type: league.type,
      scoreFormat: league.scoreFormat,
      stage: { ...ladder.stage!, closed: STAGE_CLOSED[league.id] ?? false },
      division: division.number,
      sides,
      score: recorded?.score ?? null,
      notPlayed: game.notPlayed,
      history: recorded?.history ?? [],
      yourPlayerIds,
      canManage,
      managers: league.managers.map(({ name }) => ({ name })),
      timeZone: TIME_ZONE,
      today: FIXTURE_TODAY,
      // Arranging it (#9): the days that suit everyone reach only its own Players, while it has no score.
      date: game.date ?? null,
      startTime: game.startTime ?? null,
      court: game.court ?? null,
      dateSetBy: fixtureDateSetBy[game.id] ?? null,
      availability:
        yourPlayerIds.length > 0 && !game.score && !game.notPlayed && !STAGE_CLOSED[league.id]
          ? (fixtureGameAvailability[game.id] ?? { shared: [], notGiven: [] })
          : null,
    });
  }),

  // An Admin's list of Users (#116, #134, #245): newest first, searchable, by Club, paged.
  http.get("/users", ({ request, query, response }) => {
    if (!rolesOf(emailOf(request) ?? "").admin) return response(403).json(forbidden, problemJson);
    const search = (query.get("q") ?? "").trim().toLowerCase();
    const clubId = query.get("clubId");
    const offset = Number(query.get("offset") ?? 0);
    const limit = Number(query.get("limit") ?? 50);
    const inClub = (user: Schemas["UserSummary"]) =>
      !clubId ||
      user.clubs.some((held) => held.clubId === clubId) ||
      (fixtureMembers[clubId]?.members ?? []).some((m) => m.email === user.email);
    const matching = fixtureUsers
      .filter((user) => inClub(user))
      .filter((user) => !search || user.email.includes(search) || user.players.some((p) => p.name.toLowerCase().includes(search)))
      .sort((a, b) => Number(b.email === search) - Number(a.email === search));
    return response(200).json({ users: matching.slice(offset, offset + limit), page: { total: matching.length, offset, limit } });
  }),

  // Not in the OpenAPI contract: the real API's liveness check, which web's /health asks.
  http.untyped.get("/health", () => HttpResponse.text("Healthy")),
];

