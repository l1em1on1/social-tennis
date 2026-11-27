import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { expect, within } from "storybook/test";
import { StandingsTable } from "./standings-table";
import { Card } from "./ui/card";

const ids = ["t1", "t2", "t3", "t4"];
const names = { t1: "Tom Reid", t2: "Omar Ali", t3: "Josh Lee", t4: "Ravi Rao" };
const row = (teamId: string, rank: number, won: number, lost: number, gamesFor: number, gamesAgainst: number, move: "Up" | "Down" | "Stays" | null, tied = false, points?: number) => ({
  teamId, rank, played: won + lost, won, lost, gamesFor, gamesAgainst, tied, move, points,
});

const meta = {
  title: "Ladder/StandingsTable",
  component: StandingsTable,
  args: {
    number: 2,
    names,
    mine: ["t3"],
    standings: [
      row("t2", 1, 2, 0, 12, 5, "Up"),
      row("t3", 2, 1, 1, 10, 9, "Stays"),
      row("t1", 3, 1, 1, 8, 10, "Stays"),
      row("t4", 4, 0, 2, 6, 12, "Down"),
    ],
  },
} satisfies Meta<typeof StandingsTable>;
export default meta;
type Story = StoryObj<typeof meta>;

const rows = (canvas: Parameters<NonNullable<Story["play"]>>[0]["canvas"]) =>
  within(canvas.getByRole("table", { name: "Division 2 standings" })).getAllByRole("row").slice(1);

/** Mid-Stage: first goes up, last goes down, the Acting Player's Team in bold. */
export const MidStage: Story = {
  play: async ({ canvas }) => {
    const [first, , , last] = rows(canvas);
    await expect(first).toHaveTextContent("1Omar Ali220+7▲ up");
    await expect(last).toHaveTextContent("4Ravi Rao202-6▼ down");
    // The name too, not only the numbers: a th is bold unless told otherwise.
    const weight = (name: string) => getComputedStyle(canvas.getByRole("rowheader", { name })).fontWeight;
    await expect(weight("Josh Lee")).toBe("700");
    await expect(weight("Omar Ali")).toBe("500");
  },
};

/**
 * Two level at the top: Omar beat Ravi 6–2 and Josh beat Tom 6–2, and they haven't played each
 * other, so who goes up is the Manager's to settle. Tom beat Ravi 6–3, so the bottom is decided.
 */
export const TiedForFirst: Story = {
  args: {
    standings: [
      row("t2", 1, 1, 0, 6, 2, null, true),
      row("t3", 1, 1, 0, 6, 2, null, true),
      row("t1", 3, 1, 1, 8, 9, "Stays"),
      row("t4", 4, 0, 2, 5, 12, "Down"),
    ],
  },
  play: async ({ canvas }) => {
    const [first, second] = rows(canvas);
    await expect(first).toHaveTextContent("=1Omar Ali");
    await expect(first).toHaveTextContent("tied");
    await expect(second).toHaveTextContent("=1Josh Lee");
  },
};

/** Before any score: no places or arrows yet, and it says why. */
export const NoScoresYet: Story = {
  args: { standings: ids.map((teamId) => row(teamId, 1, 0, 0, 0, 0, null, true)) },
  play: async ({ canvas }) => {
    await expect(rows(canvas)[0]).toHaveTextContent("–Tom Reid0000");
    await expect(canvas.getByText("No scores yet: the table fills in as games are played.")).toBeVisible();
  },
};

/** Whether a word sits on one line. Its text node's boxes: one per line it is broken over. */
function onOneLine(cell: HTMLElement, word: string) {
  const text = [...cell.childNodes].find((node) => node.textContent?.includes(word))!;
  const range = document.createRange();
  range.setStart(text, text.textContent!.indexOf(word));
  range.setEnd(text, text.textContent!.indexOf(word) + word.length);
  return range.getClientRects().length === 1;
}

/**
 * On a card on a phone, as the ladder shows it. A name breaks between its
 * words, and inside one only when that word alone is wider than the column:
 * then it must, or the table would be wider than the card.
 */
export const LongNames: Story = {
  args: { names: { ...names, t2: "OpponentWithAnUnusuallyLongUnbrokenName", t3: "Christopher Featherstone" } },
  decorators: [
    (Story) => (
      <Card title="Division 2">
        <Story />
      </Card>
    ),
  ],
  play: async ({ canvas }) => {
    // Measured in the page's own font: the fallback's letters are narrower.
    await document.fonts.ready;
    const table = canvas.getByRole("table", { name: "Division 2 standings" });
    await expect(table.scrollWidth).toBeLessThanOrEqual(table.parentElement!.clientWidth);
    // The Acting Player's own row, in bold: the widest these words get.
    const mine = canvas.getByRole("rowheader", { name: "Christopher Featherstone" });
    await expect(onOneLine(mine, "Christopher")).toBe(true);
    await expect(onOneLine(mine, "Featherstone")).toBe(true);
  },
};

/**
 * A Bonus points League (#214): a point a win, a point a game played and one
 * more for playing all three. Tom v Ravi isn't played yet. Christopher has
 * played all three, so one win and the bonus put him above Tom's two wins
 * from two. A column wider, the table still fits a phone card without
 * breaking a name inside a word.
 */
export const BonusPoints: Story = {
  args: {
    names: { ...names, t3: "Christopher Featherstone" },
    standings: [
      row("t2", 1, 2, 1, 15, 9, "Up", false, 6),
      row("t3", 2, 1, 2, 12, 16, "Stays", false, 5),
      row("t1", 3, 2, 0, 12, 7, "Stays", false, 4),
      row("t4", 4, 0, 2, 5, 12, "Down", false, 2),
    ],
  },
  decorators: LongNames.decorators,
  play: async ({ canvas }) => {
    const [first, second] = rows(canvas);
    await expect(canvas.getByRole("columnheader", { name: "Pts" })).toBeVisible();
    await expect(first).toHaveTextContent("1Omar Ali321+66▲ up");
    await expect(second).toHaveTextContent("2Christopher Featherstone312-45stays");
    await expect(canvas.getByText(/^A point a win, a point a game played and one more for playing them all/)).toBeVisible();
    await document.fonts.ready;
    const table = canvas.getByRole("table", { name: "Division 2 standings" });
    await expect(table.scrollWidth).toBeLessThanOrEqual(table.parentElement!.clientWidth);
    const mine = canvas.getByRole("rowheader", { name: "Christopher Featherstone" });
    await expect(onOneLine(mine, "Christopher")).toBe(true);
    await expect(onOneLine(mine, "Featherstone")).toBe(true);
  },
};

/**
 * A Foursomes Division (#14): each Player's Games and the games they banked,
 * which alone rank them, under the Players' names.
 */
export const Foursomes: Story = {
  args: {
    foursomes: { gameNotPlayed: false },
    standings: [row("t2", 1, 2, 0, 18, 11, "Up"), row("t3", 2, 1, 1, 15, 14, "Stays"), row("t1", 3, 1, 1, 14, 15, "Stays"), row("t4", 4, 0, 2, 11, 18, "Down")],
  },
  play: async ({ canvas }) => {
    const [first, , , last] = rows(canvas);
    await expect(canvas.getByRole("columnheader", { name: "Player" })).toBeVisible();
    await expect(canvas.getByRole("columnheader", { name: "Games" })).toBeVisible();
    await expect(canvas.queryByRole("columnheader", { name: "±" })).toBeNull();
    await expect(first).toHaveTextContent("1Omar Ali218▲ up");
    await expect(last).toHaveTextContent("4Ravi Rao211▼ down");
    await expect(canvas.getByText(/^Ranked by games banked/)).toBeVisible();
  },
};

/** A Foursomes Division with a game not played: everyone level, for the Manager to order (#14). */
export const FoursomesGameNotPlayed: Story = {
  args: {
    foursomes: { gameNotPlayed: true },
    standings: [row("t2", 1, 2, 0, 12, 4, null, true), row("t3", 1, 1, 1, 9, 9, null, true), row("t1", 1, 1, 1, 8, 9, null, true), row("t4", 1, 0, 2, 5, 12, null, true)],
  },
  play: async ({ canvas }) => {
    await expect(rows(canvas)[0]).toHaveTextContent("=1Omar Ali212tied");
    await expect(canvas.getByText(/^A game wasn't played, so the Manager decides who moves\./)).toBeVisible();
  },
};
