import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { expect } from "storybook/test";
import { AvailabilityForm } from "./availability-form";

const meta = {
  title: "Forms/AvailabilityForm",
  component: AvailabilityForm,
  args: {
    leagueId: "00000000-0000-4000-8000-000000000001",
    playerId: "00000000-0000-4000-8000-000000000002",
    firstDay: "2026-11-01",
    lastDay: "2026-11-30",
    dates: ["2026-11-21", "2026-11-24"],
  },
} satisfies Meta<typeof AvailabilityForm>;
export default meta;
type Story = StoryObj<typeof meta>;

/** November, Monday first: the 1st is a Sunday, so six gaps before it; the days already ticked show as such. */
export const OneMonth: Story = {
  play: async ({ canvas }) => {
    await expect(canvas.getByRole("group", { name: "November 2026" })).toBeVisible();
    await expect(canvas.getAllByRole("checkbox")).toHaveLength(30);
    await expect(canvas.getByRole("checkbox", { name: "Tuesday 24 November" })).toBeChecked();
    await expect(canvas.getByRole("checkbox", { name: "Wednesday 25 November" })).not.toBeChecked();
  },
};

/** A Stage held past its deadline takes days into the next month (#9): each month is its own group. */
export const TwoMonths: Story = {
  args: { firstDay: "2026-11-20", lastDay: "2026-12-10" },
  play: async ({ canvas, userEvent }) => {
    await expect(canvas.getAllByRole("group")).toHaveLength(2);
    const day = canvas.getByRole("checkbox", { name: "Saturday 5 December" });
    await userEvent.click(day);
    await expect(day).toBeChecked();
  },
};
