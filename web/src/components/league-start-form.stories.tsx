import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { expect, within } from "storybook/test";
import { proposeDivisions } from "@/lib/league";
import { LeagueStartForm } from "./league-start-form";

const id = (n: number) => `00000000-0000-4000-8000-0000000000${String(n).padStart(2, "0")}`;
const names = ["Tom Reid", "Omar Ali", "Josh Lee", "Ravi Rao", "Gus Ward", "Dev Shah", "Liam Moss", "Nate Cole", "Ben Hart", "Kit Moore"];
const singles = (count: number) => names.slice(0, count).map((name, index) => ({ teamId: id(index + 1), names: [name] }));

const meta = {
  title: "Forms/LeagueStartForm",
  component: LeagueStartForm,
  args: {
    leagueId: "00000000-0000-4000-8000-000000000001",
    // Ten Teams in Divisions of about 4: the API proposes 3, 3 and 4.
    competitors: singles(10),
    divisions: proposeDivisions(10, 4),
    unpaired: [],
    waitingRequestCount: 0,
    firstStage: { startDate: "2026-12-01", endDate: "2026-12-31" },
  },
} satisfies Meta<typeof LeagueStartForm>;
export default meta;
type Story = StoryObj<typeof meta>;

const division = (canvas: Parameters<NonNullable<Story["play"]>>[0]["canvas"], n: number) =>
  within(canvas.getByRole("list", { name: `Division ${n}` })).getAllByRole("listitem");

/** Ten in Divisions of about 4: no Bye, the bigger Division at the bottom, and each Division's Games. */
export const Proposed: Story = {
  play: async ({ canvas }) => {
    await expect([1, 2, 3].map((n) => division(canvas, n).length)).toEqual([3, 3, 4]);
    await expect(canvas.getByRole("heading", { name: /Division 3 · 4 Teams · 6 Games/ })).toBeVisible();
  },
};

/** Moving the line under Division 1 down gives it a fourth Team from Division 2; the button keeps focus. */
export const LineMoved: Story = {
  play: async ({ canvas, userEvent }) => {
    // Division 2 has 3, so it can give one: 4, 2 and 4.
    await userEvent.click(canvas.getAllByRole("button", { name: "Down one" })[0]);
    await expect([1, 2, 3].map((n) => division(canvas, n).length)).toEqual([4, 2, 4]);
    await expect(canvas.getByRole("status")).toHaveTextContent("Division 1 now has 4, Division 2 has 2.");
  },
};

/** Fewer Divisions re-splits the ladder evenly; a Team moved up crosses into the Division above. */
export const Reordered: Story = {
  play: async ({ canvas, userEvent }) => {
    await userEvent.click(canvas.getByRole("button", { name: "Fewer Divisions" }));
    await expect([1, 2].map((n) => division(canvas, n).length)).toEqual([5, 5]);
    await userEvent.click(canvas.getByRole("button", { name: "Move Dev Shah up" }));
    await expect(canvas.getByRole("button", { name: "Move Dev Shah up" })).toHaveFocus();
    await expect(division(canvas, 1).map((row) => row.textContent)).toContainEqual(expect.stringContaining("Dev Shah"));
    await expect(canvas.getByRole("status")).toHaveTextContent("Dev Shah moved up to place 5, Division 1.");
  },
};

/** Leaving Teams out keeps the number of Divisions and re-splits what's left; they're listed apart, to put back. */
export const LeftOut: Story = {
  args: { competitors: singles(6), divisions: proposeDivisions(6, 4) },
  play: async ({ canvas, userEvent }) => {
    await userEvent.click(canvas.getByRole("button", { name: "Leave out Tom Reid" }));
    await expect([1, 2].map((n) => division(canvas, n).length)).toEqual([2, 3]);
    await expect(within(canvas.getByRole("list", { name: "Left out" })).getByRole("listitem")).toHaveTextContent("Tom Reid");
    // Its row left the ladder; focus follows it to Put back, and back again.
    await expect(canvas.getByRole("button", { name: "Put back Tom Reid" })).toHaveFocus();
    await userEvent.click(canvas.getByRole("button", { name: "Put back Tom Reid" }));
    await expect(canvas.getByRole("button", { name: "Move Tom Reid up" })).toHaveFocus();
  },
};

/** Doubles: the Players without a partner are named, since starting removes them, and waiting requests are flagged. */
export const DoublesWithUnpaired: Story = {
  args: {
    competitors: [
      { teamId: id(1), names: ["Alex Morgan", "Jo Bennett"] },
      { teamId: id(2), names: ["Priya Shah", "Sam Morgan"] },
    ],
    divisions: [2],
    unpaired: [
      { playerId: id(20), name: "Chris Lee" },
      { playerId: id(21), name: "Dana Price" },
    ],
    waitingRequestCount: 2,
  },
};

/** Fewer than two on the ladder: Confirm is off. */
export const TooFew: Story = {
  args: { competitors: singles(3), divisions: [3] },
  play: async ({ canvas, userEvent }) => {
    await userEvent.click(canvas.getByRole("button", { name: "Leave out Tom Reid" }));
    await userEvent.click(canvas.getByRole("button", { name: "Leave out Omar Ali" }));
    await expect(canvas.getByRole("button", { name: "Confirm and start" })).toBeDisabled();
    // Too few for a Division, the one left on the ladder is still there to act on.
    await expect(within(canvas.getByRole("list", { name: "On the ladder" })).getByRole("listitem")).toHaveTextContent("Josh Lee");
    await expect(canvas.getByRole("button", { name: "Leave out Josh Lee" })).toBeVisible();
  },
};
