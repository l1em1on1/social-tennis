import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { expect, within } from "storybook/test";
import { StageCloseForm } from "./stage-close-form";

const team = (teamId: string, name: string, rank: number, record: string) => ({ teamId, name, rank, record });

const meta = {
  title: "Forms/StageCloseForm",
  component: StageCloseForm,
  args: {
    leagueId: "00000000-0000-4000-8000-000000000001",
    stageNumber: 1,
    stageEnd: "2026-11-30",
    leagueEnd: "2027-03-31",
    // Division 2's Game wasn't played: Omar and Tom are tied for who goes up.
    divisions: [
      { number: 1, teams: [team("a", "Josh Lee", 1, "1–0, +4"), team("b", "Ravi Rao", 2, "0–1, -4")], ties: [] },
      { number: 2, teams: [team("c", "Omar Ali", 1, "0–0, 0"), team("d", "Tom Reid", 1, "0–0, 0")], ties: [["c", "d"]] },
    ],
    waiting: [{ teamId: "e", name: "Dev Shah" }],
    nextStage: { startDate: "2026-12-01", endDate: "2026-12-31" },
  },
} satisfies Meta<typeof StageCloseForm>;
export default meta;
type Story = StoryObj<typeof meta>;

type Canvas = Parameters<NonNullable<Story["play"]>>[0]["canvas"];

const order = (canvas: Canvas, division: number) =>
  within(canvas.getByRole("list", { name: `Division ${division} order` })).getAllByRole("listitem");

/** Where everyone goes: Ravi down, Omar up for now, and the next Stage's dates proposed. */
export const Proposed: Story = {
  play: async ({ canvas }) => {
    await expect(order(canvas, 1)[1]).toHaveTextContent("Ravi Rao · 0–1, -4▼ to Division 2");
    await expect(order(canvas, 2)[0]).toHaveTextContent("Omar Ali · 0–0, 0, tied▲ to Division 1");
    await expect(canvas.getByLabelText("Starts")).toHaveValue("2026-12-01");
    await expect(canvas.getByRole("button", { name: "Close Stage 1 and open Stage 2" })).toBeEnabled();
  },
};

/** The Manager orders the tie: Tom goes up instead. Only the tied Teams have arrows. */
export const TieOrdered: Story = {
  play: async ({ canvas, userEvent }) => {
    await expect(canvas.queryByRole("button", { name: "Move Josh Lee up" })).not.toBeInTheDocument();
    await expect(canvas.getByRole("button", { name: "Move Omar Ali up" })).toBeDisabled();
    await userEvent.click(canvas.getByRole("button", { name: "Move Tom Reid up" }));
    await expect(order(canvas, 2)[0]).toHaveTextContent("Tom Reid · 0–0, 0, tied▲ to Division 1");
    await expect(order(canvas, 2)[1]).toHaveTextContent("Omar Ali · 0–0, 0, tiedstays");
  },
};

/** A waiting Team ticked: it joins the bottom of the bottom Division. */
export const TeamJoining: Story = {
  play: async ({ canvas, userEvent }) => {
    await userEvent.click(canvas.getByRole("checkbox", { name: "Dev Shah" }));
    await expect(canvas.getByRole("group", { name: "Teams waiting for a place" })).toHaveTextContent("Ticked Teams start Stage 2 at the bottom of Division 2.");
  },
};

/** The League's last Stage: closing ends the League, nobody moves, and there are no dates to set. */
export const LastStage: Story = {
  args: { nextStage: null, waiting: [] },
  play: async ({ canvas }) => {
    await expect(canvas.getByRole("note")).toHaveTextContent("Closing it ends the League, and nobody moves.");
    await expect(canvas.queryByLabelText("Starts")).not.toBeInTheDocument();
    await expect(canvas.getByRole("button", { name: "Close Stage 1 and end the League" })).toBeEnabled();
  },
};
