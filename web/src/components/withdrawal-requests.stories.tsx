import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { expect, within } from "storybook/test";
import { WithdrawalRequests } from "./withdrawal-requests";

const meta = {
  title: "League/WithdrawalRequests",
  component: WithdrawalRequests,
  args: {
    leagueId: "00000000-0000-4000-8000-000000000001",
    stageNumber: 1,
    foursomes: false,
    requests: [
      { id: "00000000-0000-4000-8000-000000000011", playerName: "Vic Patel", teamName: "Vic Patel", requestedAt: "2026-12-10T09:00:00Z" },
      {
        id: "00000000-0000-4000-8000-000000000012",
        playerName: "Ann Lee",
        teamName: "Ann Lee & Bob Ray",
        requestedAt: "2026-12-12T09:00:00Z",
      },
    ],
  },
} satisfies Meta<typeof WithdrawalRequests>;
export default meta;
type Story = StoryObj<typeof meta>;

/** Who asked and when; in doubles, for which Team. Each Accept is named for its Team. */
export const Waiting: Story = {
  play: async ({ canvas }) => {
    const rows = within(canvas.getByRole("list", { name: "Requests to leave" })).getAllByRole("listitem");
    await expect(rows[0]).toHaveTextContent("Vic PatelAsked 10 Dec 2026");
    await expect(rows[1]).toHaveTextContent("Ann LeeFor Ann Lee & Bob Ray. Asked 12 Dec 2026");
    await expect(canvas.getByRole("button", { name: "Accept Ann Lee & Bob Ray leaving" })).toBeEnabled();
    await expect(canvas.getByRole("button", { name: "Decline Ann Lee's request to leave" })).toBeEnabled();
    await expect(canvas.getByText("2 waiting")).toBeVisible();
  },
};

/** Accept opens a sheet that says what follows before anything changes. */
export const AcceptSheet: Story = {
  play: async ({ canvas, userEvent }) => {
    await userEvent.click(canvas.getByRole("button", { name: "Accept Vic Patel leaving" }));
    const sheet = within(canvas.getByRole("dialog", { name: "Vic Patel leaving?" }));
    await expect(sheet.getByText(/Vic Patel keeps its place until Stage 1 closes/)).toBeVisible();
    await expect(sheet.getByText(/it leaves the ladder, or you name a player waiting for a place to take it/)).toBeVisible();
    await expect(sheet.getByRole("button", { name: "Accept: Vic Patel leaves the league" })).toBeEnabled();
  },
};

/** In Foursomes, a replacement is named at the close: the sheet says so. */
export const Foursomes: Story = {
  args: { foursomes: true },
  play: async ({ canvas, userEvent }) => {
    await userEvent.click(canvas.getByRole("button", { name: "Accept Vic Patel leaving" }));
    const sheet = within(canvas.getByRole("dialog", { name: "Vic Patel leaving?" }));
    await expect(sheet.getByText(/so the Division stays 4/)).toBeVisible();
  },
};

/** Nobody asking: no card at all. */
export const NoneWaiting: Story = {
  args: { requests: [] },
  play: async ({ canvas }) => {
    await expect(canvas.queryByText("Asking to leave")).toBeNull();
  },
};
