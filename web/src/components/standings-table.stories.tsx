import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { expect, within } from "storybook/test";
import { StandingsTable } from "./standings-table";

const ids = ["t1", "t2", "t3", "t4"];
const names = { t1: "Tom Reid", t2: "Omar Ali", t3: "Josh Lee", t4: "Ravi Rao" };
const row = (teamId: string, rank: number, won: number, lost: number, gamesFor: number, gamesAgainst: number, move: "Up" | "Down" | "Stays" | null, tied = false) => ({
  teamId, rank, played: won + lost, won, lost, gamesFor, gamesAgainst, tied, move,
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
