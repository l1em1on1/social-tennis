// PROTOTYPE (#254), throwaway: the guides' made-up club, photographed in each look. Not for main.
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { expect, test, type Browser, type Page } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";
import { takeNewestMagicLink } from "../support/auth";

const OUT = process.env.LOOKS_OUT ?? "/looks";
const CSS = process.env.LOOKS_CSS ?? "/css";
const API = process.env.E2E_API_URL ?? "http://api:8080";
const LOOKS = (process.env.LOOKS ?? "today,court,sticker,berry,seventies,board").split(",");
const MODES = ["light", "dark"] as const;
const style = ["fonts.css", "tokens.css", "treatments.css"].map((file) => readFileSync(`${CSS}/${file}`, "utf8")).join("\n");

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


type Buttons = "words" | "icon" | "both";

/** Redraws the row buttons in place, over the app's own markup; "words" puts them back. */
function redraw(kind: Buttons) {
  const win = window as unknown as { kept?: Map<Element, { html: string; cls: string; css: string }>; moved?: (() => void)[] };
  win.kept ??= new Map();
  for (const back of (win.moved ?? []).reverse()) back();
  win.moved = [];
  for (const [element, was] of win.kept) {
    element.innerHTML = was.html;
    element.className = was.cls;
    (element as HTMLElement).style.cssText = was.css;
  }
  win.kept.clear();
  if (kind === "words") return;
  const svg = (d: string) =>
    `<svg aria-hidden="true" viewBox="0 0 24 24" style="width:22px;height:22px;flex:none;fill:none;stroke:currentColor;stroke-width:2;stroke-linecap:round;stroke-linejoin:round"><path d="${d}"/></svg>`;
  const icons = {
    out: "M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM2 21a7 7 0 0 1 14 0M16 11h6",
    up: "M6 15l6-6 6 6",
    down: "M6 9l6 6 6-6",
    pencil: "M4 20l1-4L16 5l3 3L8 19zM14 7l3 3",
    door: "M14 4H5v16h9M10 12h11M17 8l4 4-4 4",
    next: "M9 5l7 7-7 7",
  };
  const set = (element: Element, html: string, css: string) => {
    win.kept!.set(element, { html: element.innerHTML, cls: element.className, css: (element as HTMLElement).style.cssText });
    element.innerHTML = html;
    (element as HTMLElement).style.cssText = css;
  };
  const plain = "border:0!important;box-shadow:none!important;background:transparent;color:inherit;padding:0;";
  const square = `${plain}width:44px;min-width:44px;height:44px;`;
  const tall = `${plain}width:52px;min-width:52px;height:52px;display:inline-flex;flex-direction:column;gap:1px;`;
  const word = (text: string) => `<span style="font-size:11px;font-weight:600;line-height:1;white-space:nowrap">${text}</span>`;
  const act = (selector: string, icon: string, text: string) => {
    for (const element of document.querySelectorAll(selector)) {
      if (kind === "icon") set(element, svg(icon), square);
      else set(element, svg(icon) + word(text), tall);
    }
  };
  act('[id^="leave-out-"]', icons.out, "Leave out");
  for (const row of document.querySelectorAll<HTMLElement>("main form ol li > div.flex")) row.style.alignItems = "center";
  for (const element of document.querySelectorAll('[id^="move-up-"], [id^="line-up-"]')) set(element, svg(icons.up), square);
  for (const element of document.querySelectorAll('[id^="move-down-"], [id^="line-down-"]')) set(element, svg(icons.down), square);
  for (const button of document.querySelectorAll("main button")) {
    const text = button.textContent ?? "";
    if (/^Rename/.test(text) && button.ariaHasPopup) act2(button, icons.pencil, "Rename");
    if (/^Edit/.test(text)) {
      act2(button, icons.pencil, "Edit");
      // Beside the player's name, rather than on a row of its own.
      const row = button.closest("li")?.querySelector("div.min-h-11");
      const { parentElement: home, nextSibling: after } = button;
      const group = home!;
      row?.append(button);
      const shown = group.style.display;
      if (!group.querySelector("form")) group.style.display = "none";
      win.moved.push(() => { group.style.display = shown; group.insertBefore(button, after); });
    }
    if (/^Leave/.test(text) && button.ariaHasPopup) act2(button, icons.door, "Leave");
  }
  function act2(element: Element, icon: string, text: string) {
    if (kind === "icon") set(element, svg(icon), square);
    else set(element, svg(icon) + word(text), tall);
  }
  // "Move line" keeps to the Leave out column, so its arrows stay under the others.
  for (const label of document.querySelectorAll("span.w-28")) set(label, label.innerHTML, `width:${kind === "icon" ? 44 : 52}px;font-size:11px;line-height:1.1;white-space:normal`);
  // Settle: the whole row opens the game.
  for (const link of document.querySelectorAll('ul[aria-label="Games with no score"] a')) {
    if (kind === "icon") set(link, svg(icons.next), square);
    else set(link, `<span style="font-size:13px;font-weight:600">Settle</span>${svg(icons.next)}`, `${plain}height:44px;gap:2px;display:inline-flex;`);
  }
}

async function shoot(page: Page, screen: string, { buttons = ["words"], top, wide = false, looks = LOOKS }: { buttons?: Buttons[]; top?: string; wide?: boolean; looks?: string[] } = {}) {
  await page.evaluate(() => (document.activeElement instanceof HTMLElement && !document.activeElement.closest("dialog") ? document.activeElement.blur() : undefined));
  await page.addStyleTag({ content: style });
  for (const look of looks) {
    for (const mode of MODES) {
      await page.emulateMedia({ colorScheme: mode });
      await page.evaluate(
        ([l, s]) => {
          document.documentElement.dataset.screen = s;
          if (l === "today") delete document.documentElement.dataset.look;
          else document.documentElement.dataset.look = l;
        },
        [look, screen],
      );
      await page.evaluate(() => document.fonts.ready);
      for (const violation of (await new AxeBuilder({ page }).withRules(["color-contrast"]).analyze()).violations) {
        for (const node of violation.nodes) console.log(`AXE ${screen} ${look} ${mode}: ${node.target} ${node.any[0]?.message}`);
      }
      for (const kind of buttons) {
        await page.evaluate(redraw, kind);
        await page.evaluate((selector) => {
          const target = selector ? document.querySelector(selector) : null;
          window.scrollTo(0, target ? target.getBoundingClientRect().top + window.scrollY - 92 : 0);
        }, top ?? null);
        await page.waitForTimeout(120);
        const over = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
        if (over > 0) console.log(`WIDER by ${over}px: ${screen} ${look} ${mode} ${kind}`);
        const file = [screen, wide && "wide", look, mode, buttons.length > 1 && kind].filter(Boolean).join("-");
        writeFileSync(`${OUT}/${file}.webp`, await toWebp(await page.screenshot({ animations: "disabled", caret: "hide" })));
      }
      await page.evaluate(redraw, "words" as Buttons);
    }
  }
  await page.emulateMedia({ colorScheme: "light" });
  await page.reload();
}

test("the looks", async ({ browser }) => {
  mkdirSync(OUT, { recursive: true });
  converter = await newPage(browser);
  const admin = await session("admin@example.com");
  const club = await call<{ id: string }>(admin, "/clubs", "POST", { name: CLUB });
  // Morgan runs the club; Jo and Alex have an account and a player; everyone is a member.
  const userId = async (email: string) =>
    (await call<{ users: { id: string; email: string }[] }>(admin, `/users?q=${encodeURIComponent(email)}`)).users.find((user) => user.email === email)!.id;
  const everyone = [people.jo.name, people.alex.name, ...players];
  const address = (name: string) => `${name.toLowerCase().replace(" ", ".")}@example.com`;
  for (const name of everyone) await call(admin, `/clubs/${club.id}/players`, "POST", { name, email: address(name) });
  await session(people.jo.email);
  await call(admin, `/clubs/${club.id}/managers/${await userId(people.jo.email)}`, "PUT");

  const jo = await newPage(browser);
  await signIn(jo, people.jo.email);
  await jo.goto(`/manage/leagues/new?club=${club.id}`);
  await jo.getByLabel("Name", { exact: true }).fill(LEAGUE);
  await jo.getByLabel("Starts").fill(starts);
  await jo.getByLabel("Ends").fill(ends);
  await jo.getByRole("button", { name: "Create League" }).click();
  await expect(jo.getByRole("heading", { name: LEAGUE, level: 1 })).toBeVisible();
  const leaguePath = new URL(jo.url()).pathname;
  const leagueId = leaguePath.split("/").at(-1)!;
  const addable = await call<{ players: { playerId: string; name: string }[] }>(admin, `/leagues/${leagueId}/addable-players`);
  for (const name of [...players, people.alex.name]) {
    await call(admin, `/leagues/${leagueId}/participants/${addable.players.find((player) => player.name === name)!.playerId}`, "PUT");
  }

  const all: Buttons[] = ["words", "icon", "both"];
  await jo.goto(`${leaguePath}/start`);
  await expect(jo.getByRole("heading", { name: "Starting ladder" })).toBeVisible();
  await shoot(jo, "start", { buttons: all, top: "main form h2" });
  await jo.getByRole("button", { name: "Confirm and start" }).click();
  await expect(jo.getByText("has started.")).toBeVisible();

  interface Ladder {
    divisions: { games: { id: string; firstTeamId: string; secondTeamId: string }[]; competitors: { teamId: string; players: { playerId: string; name: string }[] }[] }[];
  }
  const ladder = await call<Ladder>(admin, `/leagues/${leagueId}/ladder`);
  const games = ladder.divisions.flatMap((division) =>
    division.games.map((game) => ({
      id: game.id,
      sides: [game.firstTeamId, game.secondTeamId].map((team) => division.competitors.find((entry) => entry.teamId === team)!.players[0]),
    })),
  );
  const gameOf = (one: string, two: string) => games.find((game) => [one, two].every((name) => game.sides.some((side) => side.name === name)))!;
  const day = (n: number) => iso(new Date(Math.min(today.getTime(), Date.parse(starts) + n * 86_400_000)));
  const [priya, tom, nia, ella, chris, maya, ben] = players;
  const submit = async (winner: string, loser: string, won: number, lost: number, n: number) => {
    const game = gameOf(winner, loser);
    const first = game.sides[0].name === winner;
    await call(await session(address(winner)), `/games/${game.id}/score`, "POST", {
      playerId: game.sides.find((side) => side.name === winner)!.playerId,
      playedOn: day(n),
      sets: [{ first: first ? won : lost, second: first ? lost : won, firstTiebreak: null, secondTiebreak: null }],
      retirementWinner: null,
    });
  };
  await submit(people.alex.name, chris, 6, 3, 3);
  await submit(people.alex.name, maya, 6, 4, 9);
  await submit(chris, ben, 6, 1, 14);
  await submit(priya, tom, 6, 4, 5);
  await submit(priya, nia, 6, 2, 8);
  await submit(tom, ella, 6, 2, 15);

  const alex = await newPage(browser);
  await signIn(alex, people.alex.email);
  await shoot(alex, "home");
  await alex.goto(`/leagues/${leagueId}`);
  await expect(alex.getByText(/^You're in/)).toBeVisible();
  await shoot(alex, "ladder");
  await alex.goto(`/games/${gameOf(people.alex.name, chris).id}`);
  await expect(alex.getByRole("heading", { name: "Score", level: 2 })).toBeVisible();
  await shoot(alex, "match");

  await jo.goto(leaguePath);
  await expect(jo.getByRole("heading", { name: "To do before the Stage closes" })).toBeVisible();
  await shoot(jo, "manage", { buttons: all });
  await jo.goto(`/games/${gameOf(nia, ella).id}`);
  await expect(jo.getByRole("button", { name: "Mark not played (manager)" })).toBeVisible();
  await shoot(jo, "settle");

  // A child on the same login, for the Play as sheet and Profiles.
  await alex.goto("/profiles");
  await alex.getByRole("button", { name: "Add another player" }).click();
  const child = alex.getByRole("dialog", { name: "Add a player" });
  await child.getByLabel("Name").fill("Sam Carter");
  await child.getByRole("button", { name: "Add player" }).click();
  await expect(alex.getByRole("button", { name: "Edit Sam Carter" })).toBeVisible();
  await shoot(alex, "profiles", { buttons: all });
  await alex.getByRole("button", { name: /switch player/ }).click();
  await expect(alex.getByRole("dialog", { name: "Play as" })).toBeVisible();
  await shoot(alex, "playas");

  const state = await alex.context().storageState();
  const desktop = await (await browser.newContext({ viewport: { width: 1280, height: 800 }, deviceScaleFactor: 1, isMobile: false, hasTouch: false, bypassCSP: true, storageState: state, baseURL: test.info().project.use.baseURL })).newPage();
  await desktop.goto("/");
  await expect(desktop.getByRole("heading", { name: "Home", level: 1 })).toBeVisible();
  await shoot(desktop, "home", { wide: true });
  await desktop.goto(`/leagues/${leagueId}`);
  await expect(desktop.getByRole("heading", { name: LEAGUE, level: 1 })).toBeVisible();
  await shoot(desktop, "ladder", { wide: true });
});
