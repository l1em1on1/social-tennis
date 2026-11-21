import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { expect, within } from "storybook/test";
import { LeagueStartForm } from "./league-start-form";

const id = (n: number) => `00000000-0000-4000-8000-0000000000${String(n).padStart(2, "0")}`;

const meta = {
  title: "Forms/LeagueStartForm",
  component: LeagueStartForm,
  args: {
    leagueId: "00000000-0000-4000-8000-000000000001",
    competitors: [
      { teamId: id(1), names: ["Tom Reid"] },
      { teamId: id(2), names: ["Omar Ali"] },
      { teamId: id(3), names: ["Josh Lee"] },
      { teamId: id(4), names: ["Ravi Rao"] },
      { teamId: id(5), names: ["Gus Ward"] },
    ],
    unpaired: [],
    waitingRequestCount: 0,
    firstStage: { startDate: "2026-12-01", endDate: "2026-12-31" },
  },
} satisfies Meta<typeof LeagueStartForm>;
export default meta;
type Story = StoryObj<typeof meta>;

/** Five: two Divisions, and the last on the ladder has the Bye. */
export const OddNumber: Story = {};

/** Moving and leaving out change the Divisions as you go; the moved row keeps focus. */
export const Reordered: Story = {
  play: async ({ canvas, userEvent }) => {
    await userEvent.click(canvas.getByRole("button", { name: "Move Gus Ward up" }));
    await expect(canvas.getByRole("button", { name: "Move Gus Ward up" })).toHaveFocus();
    await userEvent.click(canvas.getByRole("button", { name: "Leave out Tom Reid" }));
    const rows = within(canvas.getByRole("list", { name: "Starting ladder" })).getAllByRole("listitem");
    await expect(rows.map((row) => row.textContent)).toEqual([
      expect.stringContaining("Left out"),
      expect.stringContaining("Division 1"),
      expect.stringContaining("Division 1"),
      expect.stringContaining("Division 2"),
      expect.stringContaining("Division 2"),
    ]);
    // Four on the ladder: nobody has the Bye.
    await expect(canvas.queryByText(/has the Bye/)).toBeNull();
  },
};

/** A move is announced with the place on the ladder that would be started, not counting anyone left out. */
export const AnnouncesLadderPlace: Story = {
  play: async ({ canvas, userEvent }) => {
    await userEvent.click(canvas.getByRole("button", { name: "Leave out Tom Reid" }));
    await userEvent.click(canvas.getByRole("button", { name: "Move Josh Lee up" }));
    await expect(canvas.getByRole("status")).toHaveTextContent("Josh Lee moved up to place 1 on the ladder, Division 1.");
  },
};

/** Doubles: the Players without a partner are named, since starting removes them, and waiting requests are flagged. */
export const DoublesWithUnpaired: Story = {
  args: {
    competitors: [
      { teamId: id(1), names: ["Alex Morgan", "Jo Bennett"] },
      { teamId: id(2), names: ["Priya Shah", "Sam Morgan"] },
    ],
    unpaired: [
      { playerId: id(10), name: "Chris Lee" },
      { playerId: id(11), name: "Dana Price" },
    ],
    waitingRequestCount: 2,
  },
};

/** Fewer than two on the ladder: Confirm is off. */
export const TooFew: Story = {
  play: async ({ canvas, userEvent }) => {
    for (const name of ["Tom Reid", "Omar Ali", "Josh Lee", "Ravi Rao"]) {
      await userEvent.click(canvas.getByRole("button", { name: `Leave out ${name}` }));
    }
    await expect(canvas.getByRole("button", { name: "Confirm and start" })).toBeDisabled();
  },
};
