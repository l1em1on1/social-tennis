import { mkdirSync, writeFileSync } from "node:fs";
import { expect, test, type Browser, type Locator, type Page } from "@playwright/test";
import { takeNewestMagicLink } from "../support/auth";

/**
 * The screenshots of the how-to guides (#251), in the order they are taken:
 * a club is set up, a member joins it, a league is created, started, scored and
 * closed. Everyone in it is made up. Needs an empty database.
 */

const OUT = process.env.GUIDE_OUT ?? "/guide";
const API = process.env.E2E_API_URL ?? "http://api:8080";
/** What the guides show in place of the test stack's own address. */
const SHOWN_ADDRESS = "https://your-club.example";

const CLUB = "Riverside Tennis Club";
const LEAGUE = "Winter Singles";
const people = {
  morgan: { name: "Morgan Ellis", email: "morgan.ellis@example.com" },
  jo: { name: "Jo Bennett", email: "jo.bennett@example.com" },
  alex: { name: "Alex Carter", email: "alex.carter@example.com" },
};
const players = ["Priya Shah", "Tom Okafor", "Nia Brooks", "Ella Grant", "Chris Lee", "Maya Ross", "Ben Whitfield"];

const iso = (date: Date) => date.toISOString().slice(0, 10);
const today = new Date();
/** The league runs from this month's first day to the end of the month after next, so Stage 1 is this month. */
const starts = iso(new Date(Date.UTC(today.getUTCFullYear(), today.getUTCMonth(), 1)));
const ends = iso(new Date(Date.UTC(today.getUTCFullYear(), today.getUTCMonth() + 3, 0)));

/** Calls the API as whoever the token belongs to. */
async function call<T = unknown>(token: string, path: string, method = "GET", body?: object): Promise<T> {
  const response = await fetch(`${API}${path}`, {
    method,
    headers: { Authorization: `Bearer ${token}`, ...(body ? { "Content-Type": "application/json" } : {}) },
    body: body ? JSON.stringify(body) : undefined,
  });
  if (!response.ok) throw new Error(`${method} ${path}: HTTP ${response.status} ${await response.text()}`);
  return (response.status === 204 ? undefined : await response.json()) as T;
}

/** A session of the API's own, for setting up what no guide shows. */
async function session(email: string): Promise<string> {
  const json = { "Content-Type": "application/json" };
  await fetch(`${API}/auth/magic-link`, { method: "POST", headers: json, body: JSON.stringify({ email }) });
  const token = await takeNewestMagicLink(email);
  const redeemed = await fetch(`${API}/auth/sessions`, { method: "POST", headers: json, body: JSON.stringify({ token }) });
  return ((await redeemed.json()) as { token: string }).token;
}

async function newPage(browser: Browser): Promise<Page> {
  return await (await browser.newContext()).newPage();
}

async function signIn(page: Page, email: string) {
  await page.goto("/login");
  await page.getByLabel("Email address").fill(email);
  await page.getByRole("button", { name: "Email me a sign-in link" }).click();
  await expect(page.getByRole("heading", { name: "Check your email" })).toBeVisible();
  await page.goto(`/auth/verify?token=${encodeURIComponent(await takeNewestMagicLink(email))}`);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Home", level: 1 })).toBeVisible();
}

/** A browser page of our own that turns a screenshot into WebP: less than half the PNG's size. */
let converter: Page;

async function toWebp(png: Buffer): Promise<Buffer> {
  const url = await converter.evaluate(async (base64) => {
    const image = new Image();
    image.src = `data:image/png;base64,${base64}`;
    await image.decode();
    const canvas = document.createElement("canvas");
    canvas.width = image.naturalWidth;
    canvas.height = image.naturalHeight;
    canvas.getContext("2d")!.drawImage(image, 0, 0);
    return canvas.toDataURL("image/webp", 0.75);
  }, png.toString("base64"));
  return Buffer.from(url.split(",")[1], "base64");
}

/**
 * One screenshot: `top` is scrolled to just under the header, and `ring` is
 * what the guide says to press. Rings are drawn by the page's own script
 * (CSSOM), as its Content-Security-Policy allows no style of ours. The test
 * stack's address is shown as an example one, and nothing keeps the focus.
 */
async function shot(page: Page, name: string, { top, ring }: { top?: Locator; ring?: Locator | Locator[] } = {}) {
  const rings = ring ? (Array.isArray(ring) ? ring : [ring]) : [];
  await page.evaluate((shown) => {
    if (document.activeElement instanceof HTMLElement && !document.activeElement.closest("dialog")) document.activeElement.blur();
    for (const input of document.querySelectorAll("input")) {
      if (input.value.startsWith(location.origin)) input.value = input.value.replace(location.origin, shown);
    }
  }, SHOWN_ADDRESS);
  if (top) {
    await top.evaluate((element) => window.scrollTo(0, element.getBoundingClientRect().top + window.scrollY - 72));
  } else {
    await page.evaluate(() => window.scrollTo(0, 0));
  }
  for (const target of rings) {
    await target.evaluate((element: HTMLElement) => {
      element.dataset.guideRing = element.style.boxShadow;
      // Inside its own box at the screen's edge, around it everywhere else.
      element.style.boxShadow = element.closest("nav") ? "inset 0 0 0 4px #1d4f91" : "0 0 0 3px #fffdf8, 0 0 0 7px #1d4f91";
    });
  }
  await page.waitForTimeout(250);
  writeFileSync(`${OUT}/${name}.webp`, await toWebp(await page.screenshot({ animations: "disabled", caret: "hide" })));
  for (const target of rings) {
    await target.evaluate((element: HTMLElement) => {
      element.style.boxShadow = element.dataset.guideRing ?? "";
    });
  }
}

const card = (page: Page, title: string | RegExp) =>
  page.locator("section").filter({ has: page.getByRole("heading", { level: 2, name: title }) }).first();

test("the guides' screenshots", async ({ browser }) => {
  mkdirSync(OUT, { recursive: true });
  converter = await newPage(browser);

  // Nothing the guides show: an Admin makes the club and its manager, who signs in and takes a name.
  const admin = await session("admin@example.com");
  const club = await call<{ id: string }>(admin, "/clubs", "POST", { name: CLUB });
  const morgan = await newPage(browser);
  await signIn(morgan, people.morgan.email);
  await morgan.getByLabel("Your name").fill(people.morgan.name);
  await morgan.getByRole("button", { name: "Save my name" }).click();
  await expect(morgan.getByTestId("acting-player")).toHaveText(people.morgan.name);
  const userId = async (email: string) =>
    (await call<{ users: { id: string; email: string }[] }>(admin, `/users?q=${encodeURIComponent(email)}`)).users.find(
      (user) => user.email === email,
    )!.id;
  await call(admin, `/clubs/${club.id}/managers/${await userId(people.morgan.email)}`, "PUT");

  // ---- Club manager: the club, and its join link.
  await morgan.goto("/manage");
  await expect(morgan.getByRole("heading", { name: CLUB, level: 2 })).toBeVisible();
  await shot(morgan, "club-01-manage", { ring: morgan.getByRole("link", { name: "Manage" }) });
  const turnOn = morgan.getByRole("button", { name: "Turn on the join link" });
  await shot(morgan, "club-02-link-off", { top: morgan.getByRole("heading", { name: "Join link" }), ring: turnOn });
  await turnOn.click();
  const link = morgan.getByLabel(`Link to ask to join ${CLUB}`);
  await expect(link).toBeVisible();
  const joinPath = new URL(await link.inputValue()).pathname;
  await shot(morgan, "club-03-link-on", { top: morgan.getByRole("heading", { name: "Join link" }), ring: link });

  // ---- Member: from the club's link to being in.
  const alex = await newPage(browser);
  await alex.goto(joinPath);
  await expect(alex.getByText("Sign in first, then you can ask to join the club.")).toBeVisible();
  await alex.getByLabel("Email address").fill(people.alex.email);
  await shot(alex, "member-01-sign-in", { ring: alex.getByRole("button", { name: "Email me a sign-in link" }) });
  await alex.getByRole("button", { name: "Email me a sign-in link" }).click();
  await expect(alex.getByRole("heading", { name: "Check your email" })).toBeVisible();
  await shot(alex, "member-02-check-email");
  const code = joinPath.split("/").at(-1);
  await alex.goto(`/auth/verify?token=${encodeURIComponent(await takeNewestMagicLink(people.alex.email))}&join=${code}`);
  const finish = alex.getByRole("button", { name: "Sign in", exact: true });
  await shot(alex, "member-03-finish", { ring: finish });
  await finish.click();
  await alex.getByLabel("Your name").fill(people.alex.name);
  await shot(alex, "member-04-name", { ring: alex.getByRole("button", { name: "Save my name" }) });
  await alex.getByRole("button", { name: "Save my name" }).click();
  const ask = alex.getByRole("button", { name: `Ask to join ${CLUB}` });
  await expect(ask).toBeVisible();
  await shot(alex, "member-05-ask", { ring: ask });
  await ask.click();
  await expect(alex.getByRole("heading", { name: "Waiting for approval" })).toBeVisible();
  await shot(alex, "member-06-waiting");

  // ---- Club manager: who is asking, adding someone, and the members.
  await morgan.goto("/manage");
  const approve = morgan.getByRole("button", { name: `Approve ${people.alex.name}` });
  await shot(morgan, "club-04-asking", { top: morgan.getByRole("heading", { name: /^Asking to join/ }), ring: approve });
  await approve.click();
  await expect(morgan.getByRole("heading", { name: "Members (1)" })).toBeVisible();
  const addHeading = morgan.getByRole("heading", { name: "Add someone" });
  await morgan.getByLabel("Their email address").fill(people.jo.email);
  await morgan.getByLabel("Name (optional)").fill(people.jo.name);
  await shot(morgan, "club-05-add", { top: addHeading, ring: morgan.getByRole("button", { name: "Add", exact: true }) });
  await morgan.getByRole("button", { name: "Add", exact: true }).click();
  await expect(morgan.getByRole("status")).toContainText(`${people.jo.name} is added`);
  await shot(morgan, "club-06-added", { ring: morgan.getByRole("status") });
  for (const name of players) {
    await call(admin, `/clubs/${club.id}/players`, "POST", {
      name,
      email: `${name.toLowerCase().replace(" ", ".")}@example.com`,
    });
  }
  // Someone let in ahead of time, with no player yet.
  await call(admin, `/clubs/${club.id}/invites`, "POST", { email: "dana.price@example.com" });
  // Most have signed in and said a little about themselves; the rest show "Not signed in yet".
  for (const [index, name] of players.slice(0, 5).entries()) {
    const token = await session(`${name.toLowerCase().replace(" ", ".")}@example.com`);
    const [held] = (await call<{ players: { id: string }[] }>(token, "/players")).players;
    const gender = ["Female", "Male", "Female", "Female", "Male"][index];
    await call(token, `/players/${held.id}`, "PUT", { name, gender, level: [3, 4, 2, 3, 2][index] });
  }
  const jo = await newPage(browser);
  await signIn(jo, people.jo.email);

  await morgan.goto("/manage");
  await shot(morgan, "club-07-members", { top: morgan.getByRole("heading", { name: /^Members/ }) });
  await morgan.getByRole("button", { name: `Remove ${players[6]}` }).click();
  await shot(morgan, "club-08-remove", { ring: morgan.getByRole("button", { name: "Remove from club" }) });
  await morgan.keyboard.press("Escape");
  await shot(morgan, "club-09-invites", { top: morgan.getByRole("heading", { name: "Add someone" }) });

  // ---- Club manager: a League manager.
  const role = morgan.getByRole("button", { name: `Role for ${people.jo.email}` });
  await shot(morgan, "club-10-role", { top: role, ring: role });
  await role.click();
  const make = morgan.getByRole("button", { name: /^Make league manager/ });
  await shot(morgan, "club-11-make", { ring: make });
  await make.click();
  await expect(morgan.getByRole("button", { name: `Remove ${people.jo.email} as league manager` })).toBeVisible();
  await shot(morgan, "club-12-managers", { top: morgan.getByRole("heading", { name: /^Managers/ }) });

  // ---- Member: in the club.
  await alex.goto("/");
  await expect(alex.getByText("No games yet")).toBeVisible();
  await shot(alex, "member-07-home");
  await alex.goto("/profiles");
  await alex.getByRole("button", { name: `Edit ${people.alex.name}` }).click();
  const edit = alex.getByRole("dialog", { name: `Edit ${people.alex.name}` });
  await edit.getByLabel("Plays as").selectOption("Female");
  await edit.getByLabel("Level").selectOption("3");
  await shot(alex, "member-08-profile", { ring: edit.getByRole("button", { name: "Save" }) });
  await edit.getByRole("button", { name: "Save" }).click();
  await expect(alex.getByText("Female · Level 3")).toBeVisible();

  // ---- League manager: a league of their own.
  await jo.goto("/manage");
  await expect(jo.getByText("You don't run a league here yet.")).toBeVisible();
  await shot(jo, "league-01-manage", { ring: jo.getByRole("link", { name: "New league" }) });
  await jo.getByRole("link", { name: "New league" }).click();
  await jo.getByLabel("Name", { exact: true }).fill(LEAGUE);
  await jo.getByLabel("Starts").fill(starts);
  await jo.getByLabel("Ends").fill(ends);
  await shot(jo, "league-02-new");
  await shot(jo, "league-03-format", { top: jo.getByText("When a Stage ends"), ring: jo.getByRole("button", { name: "Create League" }) });
  await jo.getByRole("button", { name: "Create League" }).click();
  await expect(jo.getByRole("heading", { name: LEAGUE, level: 1 })).toBeVisible();
  const leaguePath = new URL(jo.url()).pathname;
  const leagueId = leaguePath.split("/").at(-1)!;
  await shot(jo, "league-04-created");

  // ---- League manager: adding members; the rest are added the same way.
  const pick = jo.getByLabel("Player", { exact: true });
  await pick.selectOption({ label: players[0] });
  await shot(jo, "league-05-add", { top: card(jo, "Add a club member"), ring: jo.getByRole("button", { name: "Add to league" }) });
  await jo.getByRole("button", { name: "Add to league" }).click();
  await expect(jo.getByRole("list", { name: "Players in the league" })).toContainText(players[0]);
  const addable = await call<{ players: { playerId: string; name: string }[] }>(admin, `/leagues/${leagueId}/addable-players`);
  for (const name of players.slice(1)) {
    await call(admin, `/leagues/${leagueId}/participants/${addable.players.find((player) => player.name === name)!.playerId}`, "PUT");
  }

  // ---- Member: asking to join it.
  await alex.goto("/leagues");
  const askLeague = alex.getByRole("button", { name: /Ask to join/ });
  await shot(alex, "member-09-join-league", { top: alex.getByRole("heading", { name: "You can join" }), ring: askLeague });
  await askLeague.click();
  await expect(alex.getByRole("button", { name: /Ask to join/ })).toHaveCount(0);
  await shot(alex, "member-10-asked", { top: alex.getByRole("heading", { name: "You can join" }) });

  // ---- League manager: who is asking, and starting it.
  await jo.goto(leaguePath);
  const approveLeague = jo.getByRole("button", { name: `Approve ${people.alex.name}` });
  await shot(jo, "league-06-requests", { top: card(jo, "Join requests"), ring: approveLeague });
  await approveLeague.click();
  await expect(jo.getByRole("list", { name: "Players in the league" })).toContainText(people.alex.name);
  const startLink = jo.getByRole("link", { name: "Start the league" });
  await shot(jo, "league-07-ready", { ring: startLink });
  await startLink.click();
  await expect(jo.getByRole("heading", { name: "Starting ladder" })).toBeVisible();
  await shot(jo, "league-08-ladder", { top: jo.getByRole("heading", { name: "Starting ladder" }) });
  const start = jo.getByRole("button", { name: "Confirm and start" });
  await shot(jo, "league-09-start", { top: jo.getByRole("list", { name: "Division 2" }), ring: start });
  await start.click();
  await expect(jo.getByText("has started.")).toBeVisible();
  await shot(jo, "league-10-started");

  // ---- Member: this month's games, the ladder, and a score.
  await alex.goto("/");
  await expect(alex.getByRole("heading", { name: LEAGUE, level: 2 })).toBeVisible();
  await shot(alex, "member-11-home-games");
  await alex.goto(`/leagues/${leagueId}`);
  await expect(alex.getByText(/^You're in/)).toBeVisible();
  await shot(alex, "member-12-ladder");
  interface Ladder {
    divisions: {
      games: { id: string; firstTeamId: string; secondTeamId: string }[];
      competitors: { teamId: string; players: { playerId: string; name: string }[] }[];
    }[];
  }
  const ladder = await call<Ladder>(admin, `/leagues/${leagueId}/ladder`);
  const games = ladder.divisions.flatMap((division) =>
    division.games.map((game) => ({
      id: game.id,
      sides: [game.firstTeamId, game.secondTeamId].map(
        (team) => division.competitors.find((entry) => entry.teamId === team)!.players[0],
      ),
    })),
  );
  const gameOf = (one: string, two: string) => {
    const found = games.find((game) => [one, two].every((name) => game.sides.some((side) => side.name === name)));
    if (!found) throw new Error(`${one} and ${two} don't play each other`);
    return found;
  };
  /** A day in the Stage, and not after today, for a game played. */
  const day = (n: number) => iso(new Date(Math.min(today.getTime(), Date.parse(starts) + n * 86_400_000)));

  const [priya, tom, nia, ella, chris, maya, ben] = players;
  await alex.goto(`/games/${gameOf(people.alex.name, chris).id}`);
  const enter = alex.getByRole("link", { name: "Enter score" });
  await shot(alex, "member-13-match", { ring: enter });
  await enter.click();
  await expect(alex.getByRole("heading", { name: "Enter score" })).toBeVisible();
  const games6 = async (page: Page, name: string, count: number) => {
    const more = page.getByRole("group", { name: new RegExp(`, ${name}$`) }).first().getByRole("button", { name: "One more" });
    for (let n = 0; n < count; n++) await more.click();
  };
  await games6(alex, people.alex.name, 6);
  await games6(alex, chris, 3);
  const save = alex.getByRole("button", { name: "Save score" });
  await expect(save).toBeEnabled();
  await shot(alex, "member-14-score", { top: alex.getByLabel("Date played"), ring: save });
  await save.click();
  await expect(alex.getByRole("heading", { name: "Score", level: 2 })).toBeVisible();
  await shot(alex, "member-16-saved", { ring: alex.getByRole("link", { name: "Correct the score" }) });

  // The rest of the Stage, entered by the winners, but for two games the League manager settles.
  const submit = async (winner: string, loser: string, won: number, lost: number, n: number, tiebreak?: [number, number]) => {
    const game = gameOf(winner, loser);
    const email = winner === people.alex.name ? people.alex.email : `${winner.toLowerCase().replace(" ", ".")}@example.com`;
    const first = game.sides[0].name === winner;
    await call(await session(email), `/games/${game.id}/score`, "POST", {
      playerId: game.sides.find((side) => side.name === winner)!.playerId,
      playedOn: day(n),
      sets: [
        {
          first: first ? won : lost,
          second: first ? lost : won,
          firstTiebreak: tiebreak ? (first ? tiebreak[0] : tiebreak[1]) : null,
          secondTiebreak: tiebreak ? (first ? tiebreak[1] : tiebreak[0]) : null,
        },
      ],
      retirementWinner: null,
    });
  };
  await submit(people.alex.name, maya, 6, 4, 9);
  await alex.goto("/");
  await shot(alex, "member-17-home-results");
  await submit(people.alex.name, ben, 6, 2, 16);
  await submit(chris, maya, 6, 5, 12, [7, 4]);
  await submit(chris, ben, 6, 1, 14);
  await submit(priya, tom, 6, 4, 5);
  await submit(priya, nia, 6, 2, 8);
  await submit(priya, ella, 6, 3, 13);
  await submit(tom, nia, 6, 4, 10);
  await submit(tom, ella, 6, 5, 15, [7, 5]);
  await alex.goto(`/leagues/${leagueId}`);
  await shot(alex, "member-18-standings", { top: alex.getByRole("heading", { name: "Division 2" }) });

  // ---- League manager: what is left, and closing the Stage.
  await jo.goto(leaguePath);
  const settle = jo.getByRole("list", { name: "Games with no score" }).getByRole("link", { name: "Settle" }).first();
  await shot(jo, "league-11-todo", { top: card(jo, "To do before the Stage closes"), ring: settle });
  await jo.goto(`/games/${gameOf(nia, ella).id}`);
  const record = jo.getByRole("link", { name: "Record the score (manager)" });
  const notPlayed = jo.getByRole("button", { name: "Mark not played (manager)" });
  await shot(jo, "league-12-settle", { ring: [record, notPlayed] });
  await record.click();
  await expect(jo.getByRole("heading", { name: "Record the score" })).toBeVisible();
  await games6(jo, nia, 6);
  await games6(jo, ella, 4);
  await jo.getByRole("button", { name: "Save score" }).click();
  await expect(jo.getByRole("heading", { name: "Score", level: 2 })).toBeVisible();
  await jo.goto(`/games/${gameOf(maya, ben).id}`);
  await jo.getByRole("button", { name: "Mark not played (manager)" }).click();
  await expect(jo.getByRole("button", { name: /^Undo/ })).toBeVisible();
  await shot(jo, "league-13-not-played", { ring: jo.getByRole("button", { name: /^Undo/ }) });
  await jo.goto(`/games/${gameOf(people.alex.name, chris).id}`);
  const override = jo.getByRole("link", { name: "Override score (manager)" });
  await shot(jo, "league-14-override", { ring: override });
  await jo.goto(leaguePath);
  await shot(jo, "league-15-move", { top: card(jo, "Move a Team") });
  const close = jo.getByRole("link", { name: "Close Stage 1" });
  await shot(jo, "league-16-ready", { top: card(jo, "To do before the Stage closes"), ring: close });
  await close.click();
  await expect(jo.getByRole("heading", { name: "Close Stage 1" })).toBeVisible();
  await shot(jo, "league-17-close");
  const closeButton = jo.getByRole("button", { name: /^Close Stage 1 and open/ });
  await shot(jo, "league-18-next-stage", { top: jo.getByRole("heading", { name: "Stage 2", level: 2 }), ring: closeButton });
  await closeButton.click();
  await expect(jo.getByText(/Stage 1 is closed/)).toBeVisible();
  await shot(jo, "league-19-closed");

  // ---- Member: up a Division, and a score that can no longer change.
  await alex.goto(`/leagues/${leagueId}`);
  await expect(alex.getByText(/^You're in/)).toBeVisible();
  await shot(alex, "member-19-promoted");
  await alex.goto(`/games/${gameOf(people.alex.name, chris).id}`);
  await shot(alex, "member-20-locked");

  // ---- Member: a child on the same login.
  await alex.goto("/profiles");
  await alex.getByRole("button", { name: "Add another player" }).click();
  const child = alex.getByRole("dialog", { name: "Add a player" });
  await child.getByLabel("Name").fill("Sam Carter");
  await shot(alex, "member-21-child", { ring: child.getByRole("button", { name: "Add player" }) });
  await child.getByRole("button", { name: "Add player" }).click();
  await expect(alex.getByRole("button", { name: "Edit Sam Carter" })).toBeVisible();
  await alex.getByRole("button", { name: /switch player/ }).click();
  await shot(alex, "member-22-switch", { ring: alex.getByRole("dialog", { name: "Play as" }).getByRole("button", { name: "Sam Carter" }) });

  // ---- Club manager: every league, and who runs each.
  await morgan.goto("/manage");
  await shot(morgan, "club-13-leagues", { ring: morgan.getByRole("link", { name: LEAGUE }) });
  await morgan.goto(leaguePath);
  const give = morgan.getByLabel("Give this league to");
  await shot(morgan, "club-14-give", { top: card(morgan, /^Managers/), ring: give });
});
