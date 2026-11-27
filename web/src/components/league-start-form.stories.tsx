import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { expect, within } from "storybook/test";
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
    divisions: [3, 3, 4],
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

/** Moving the line between Divisions 1 and 2 down gives Division 1 a fourth Team; Division 2 can't go under 2, so its arrow turns off. */
export const LineMoved: Story = {
  play: async ({ canvas, userEvent }) => {
    // Division 2 has 3, so it can give one: 4, 2 and 4.
    const down = canvas.getByRole("button", { name: "Move the line between Division 1 and 2 down" });
    await userEvent.click(down);
    await expect(down).toBeDisabled();
    await expect(canvas.getByRole("button", { name: "Move the line between Division 1 and 2 up" })).toHaveFocus();
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
  args: { competitors: singles(6), divisions: [3, 3] },
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
    // Put back onto a ladder still too short for a Division, focus stays on that Team.
    await userEvent.click(canvas.getByRole("button", { name: "Leave out Josh Lee" }));
    await userEvent.click(canvas.getByRole("button", { name: "Put back Josh Lee" }));
    await expect(canvas.getByRole("button", { name: "Leave out Josh Lee" })).toHaveFocus();
  },
};

/**
 * Foursomes (#14): ten Players make two Divisions of 4, with no line to move or
 * number of Divisions to change. The two below them must be left out, or two
 * more found, before it can start.
 */
export const Foursomes: Story = {
  args: { foursomes: true, divisions: [4, 4] },
  play: async ({ canvas, userEvent }) => {
    await expect([1, 2].map((n) => division(canvas, n).length)).toEqual([4, 4]);
    await expect(canvas.getByRole("heading", { name: /Division 1 · 4 Players · 3 Games/ })).toBeVisible();
    await expect(canvas.queryByRole("button", { name: "More Divisions" })).toBeNull();
    await expect(canvas.queryByRole("button", { name: /Move the line/ })).toBeNull();
    await expect(canvas.getByRole("heading", { name: /Not in a Division · leave out 2, or add 2 more/ })).toBeVisible();
    await expect(within(canvas.getByRole("list", { name: "Not in a Division" })).getAllByRole("listitem")).toHaveLength(2);
    const confirm = canvas.getByRole("button", { name: "Confirm and start" });
    await expect(confirm).toBeDisabled();

    // Moving the ninth Player up swaps them into Division 2; leaving out the last two lets it start.
    await userEvent.click(canvas.getByRole("button", { name: "Move Ben Hart up" }));
    await expect(division(canvas, 2).map((row) => row.textContent)).toContainEqual(expect.stringContaining("Ben Hart"));
    await userEvent.click(canvas.getByRole("button", { name: "Leave out Nate Cole" }));
    await expect(canvas.getByRole("heading", { name: /Not in a Division · leave out 1, or add 3 more/ })).toBeVisible();
    await userEvent.click(canvas.getByRole("button", { name: "Leave out Kit Moore" }));
    await expect(canvas.queryByRole("list", { name: "Not in a Division" })).toBeNull();
    await expect(confirm).toBeEnabled();
  },
};
