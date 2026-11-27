import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { expect, within } from "storybook/test";
import { GameArrangement } from "./game-arrangement";

const meta = {
  title: "Games/GameArrangement",
  component: GameArrangement,
  args: {
    gameId: "00000000-0000-4000-8000-000000000001",
    leagueId: "00000000-0000-4000-8000-000000000002",
    playerId: "00000000-0000-4000-8000-000000000003",
    availability: { sharedDates: ["2026-11-24", "2026-11-27"], notGiven: [] },
    stageStart: "2026-11-01",
  },
} satisfies Meta<typeof GameArrangement>;
export default meta;
type Story = StoryObj<typeof meta>;

/** Two days suit everyone, nothing agreed yet: the date starts on the first of them. */
export const SharedDays: Story = {
  play: async ({ canvas }) => {
    await expect(canvas.queryByRole("link", { name: "Add to calendar" })).toBeNull();
    await expect(within(canvas.getByRole("list", { name: "Days that suit everyone" })).getAllByRole("listitem")).toHaveLength(2);
    await expect(canvas.getByLabelText("Agreed date")).toHaveValue("2026-11-24");
    await expect(canvas.getByRole("button", { name: "Save" })).toBeVisible();
  },
};

/** Agreed, with who set it; any of them can change it. */
export const Agreed: Story = {
  args: { date: "2026-11-27", court: "6 - astro", dateSetBy: "Chris Lee" },
  play: async ({ canvas }) => {
    await expect(canvas.getByRole("note")).toHaveTextContent("Agreed for Fri 27 Nov · 6 - astro, set by Chris Lee");
    await expect(canvas.getByRole("link", { name: "Add to calendar" })).toHaveAttribute("href", "/games/00000000-0000-4000-8000-000000000001/calendar");
    await expect(canvas.getByRole("button", { name: "Change" })).toBeVisible();
  },
};

/** No day suits everyone yet, because someone hasn't said: it names them. */
export const Waiting: Story = {
  args: { availability: { sharedDates: [], notGiven: ["Ella Grant"] } },
  play: async ({ canvas }) => {
    await expect(canvas.getByText(/^None yet: waiting for Ella Grant to say which days they're free\./)).toBeVisible();
  },
};
