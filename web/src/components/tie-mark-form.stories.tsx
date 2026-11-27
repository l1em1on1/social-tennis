import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { expect } from "storybook/test";
import { TieMarkForm } from "./tie-mark-form";

const names = { t1: "Bob Hart", t2: "Cat Shaw", t3: "Dan Moss" };

const meta = {
  title: "Ladder/TieMarkForm",
  component: TieMarkForm,
  args: {
    leagueId: "00000000-0000-4000-8000-000000000001",
    playerId: "00000000-0000-4000-8000-000000000002",
    division: 1,
    names,
    tie: { place: "Down", teamIds: ["t1", "t2", "t3"], mark: null },
  },
} satisfies Meta<typeof TieMarkForm>;
export default meta;
type Story = StoryObj<typeof meta>;

/** Three level for last once every Game is scored (#14 part 3): any of them says who goes down. */
export const Unmarked: Story = {
  play: async ({ canvas }) => {
    const group = canvas.getByRole("group", { name: "Who goes down from Division 1?" });
    await expect(group).toHaveTextContent("Bob Hart, Cat Shaw and Dan Moss are level on games.");
    await expect(canvas.getAllByRole("radio")).toHaveLength(3);
    await expect(canvas.getByRole("button", { name: "Save" })).toBeVisible();
  },
};

/** Marked: who said it, the choice ticked, and any of them can change it. */
export const Marked: Story = {
  args: { tie: { place: "Up", teamIds: ["t1", "t2"], mark: { teamId: "t2", markedBy: "Bob Hart", markedAt: "2026-11-26T20:00:00Z" } } },
  play: async ({ canvas }) => {
    await expect(canvas.getByRole("radio", { name: "Cat Shaw" })).toBeChecked();
    await expect(canvas.getByText(/^Bob Hart said/)).toHaveTextContent("Bob Hart said Cat Shaw goes up.");
    await expect(canvas.getByRole("button", { name: "Change" })).toBeVisible();
  },
};

/**
 * A middle Division where all four are level: Bob is marked to go up, so the
 * question of who goes down doesn't offer him.
 */
export const OtherWayMarked: Story = {
  args: { tie: { place: "Down", teamIds: ["t1", "t2", "t3"], mark: null }, otherWay: "t1" },
  play: async ({ canvas }) => {
    await expect(canvas.getAllByRole("radio")).toHaveLength(2);
    await expect(canvas.queryByRole("radio", { name: "Bob Hart" })).toBeNull();
  },
};
