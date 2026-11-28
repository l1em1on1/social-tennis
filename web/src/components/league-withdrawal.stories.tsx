import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { expect, within } from "storybook/test";
import type { components } from "@/lib/api/schema";
import { LeagueWithdrawal } from "./league-withdrawal";
import { Card } from "./ui/card";

type PlayerLeague = components["schemas"]["PlayerLeague"];

/** A singles League under way, with this Player in Division 2 of Stage 1. */
const running: PlayerLeague = {
  id: "00000000-0000-4000-8000-000000000001",
  clubId: "00000000-0000-4000-8000-000000000002",
  clubName: "Riverside Tennis Club",
  name: "Winter Singles",
  type: "SinglesMixed",
  structure: "Divisional",
  startDate: "2026-12-01",
  endDate: "2027-03-31",
  stageLength: { count: 1, unit: "Months" },
  stageRoll: "Manual",
  scoreFormat: { gamesPerSet: 6, setsPerGame: 3, matchTiebreakDecider: true },
  divisionSize: 4,
  standingsMode: "Standard",
  participantCount: 8,
  joined: true,
  joinRequest: null,
  partnerName: null,
  partnerRequest: null,
  stage: { number: 1, startDate: "2026-12-01", endDate: "2026-12-31" },
  place: { division: 2, opponents: [{ teamId: "t2", names: ["Omar Ali"] }] },
  withdrawal: null,
};

const meta = {
  title: "League/LeagueWithdrawal",
  component: LeagueWithdrawal,
  args: { league: running, playerId: "00000000-0000-4000-8000-000000000003" },
  decorators: [
    (Story) => (
      <Card title="Winter Singles">
        <Story />
      </Card>
    ),
  ],
} satisfies Meta<typeof LeagueWithdrawal>;
export default meta;
type Story = StoryObj<typeof meta>;

/** On the ladder: Ask to leave opens a sheet that says what follows, and only then asks. */
export const CanAsk: Story = {
  play: async ({ canvas, userEvent }) => {
    await userEvent.click(canvas.getByRole("button", { name: "Ask to leave" }));
    const sheet = within(canvas.getByRole("dialog", { name: "Leave Winter Singles?" }));
    await expect(sheet.getByText(/keep your place on the ladder until Stage 1 closes/)).toBeVisible();
    await expect(sheet.getByRole("button", { name: "Ask to leave Winter Singles" })).toBeEnabled();
  },
};

/** In doubles the Team leaves together, and the sheet names the partner. */
export const DoublesCanAsk: Story = {
  args: { league: { ...running, type: "DoublesMix", partnerName: "Sam Cole" } },
  play: async ({ canvas, userEvent }) => {
    await userEvent.click(canvas.getByRole("button", { name: "Ask to leave" }));
    await expect(canvas.getByText(/you and Sam Cole keep your place/)).toBeVisible();
  },
};

/** In Foursomes the sheet adds that the Manager names someone to take the place. */
export const FoursomesCanAsk: Story = {
  args: { league: { ...running, type: "Foursomes" } },
  play: async ({ canvas, userEvent }) => {
    await userEvent.click(canvas.getByRole("button", { name: "Ask to leave" }));
    await expect(canvas.getByText(/so the Division stays 4/)).toBeVisible();
  },
};

/** Waiting on a Manager: when it was asked, and Take back. */
export const Pending: Story = {
  args: { league: { ...running, withdrawal: { status: "Pending", requestedAt: "2026-12-10T09:00:00Z", decidedAt: null } } },
  play: async ({ canvas }) => {
    await expect(canvas.getByText("Asked to leave")).toBeVisible();
    await expect(canvas.getByText(/Asked 10 Dec 2026\. A manager decides\./)).toBeVisible();
    await expect(canvas.getByRole("button", { name: "Take back the request to leave Winter Singles" })).toBeEnabled();
    await expect(canvas.queryByRole("button", { name: "Ask to leave" })).toBeNull();
  },
};

/** A Manager said no: it says so, and the Player can still ask again. */
export const Declined: Story = {
  args: {
    league: { ...running, withdrawal: { status: "Declined", requestedAt: "2026-12-10T09:00:00Z", decidedAt: "2026-12-11T18:00:00Z" } },
  },
  play: async ({ canvas }) => {
    await expect(canvas.getByText(/A manager kept you in the league on 11 Dec 2026/)).toBeVisible();
    await expect(canvas.getByRole("button", { name: "Ask to leave" })).toBeEnabled();
  },
};

/** Withdrawn during the Stage: the place stays until the close, and nothing is left to ask. */
export const Withdrawn: Story = {
  args: {
    league: { ...running, withdrawal: { status: "Accepted", requestedAt: "2026-12-10T09:00:00Z", decidedAt: "2026-12-11T18:00:00Z" } },
  },
  play: async ({ canvas }) => {
    await expect(canvas.getByText("Withdrawn")).toBeVisible();
    await expect(canvas.getByText(/You left this league on 11 Dec 2026\. The place stays on the ladder until Stage 1 closes/)).toBeVisible();
    await expect(canvas.queryByRole("button")).toBeNull();
  },
};

/** After the close took the Team off the ladder: only that it left, and that its results stay. */
export const WithdrawnAfterTheClose: Story = {
  args: {
    league: {
      ...running,
      stage: { number: 2, startDate: "2027-01-01", endDate: "2027-01-31" },
      place: null,
      withdrawal: { status: "Accepted", requestedAt: "2026-12-10T09:00:00Z", decidedAt: "2026-12-11T18:00:00Z" },
    },
  },
  play: async ({ canvas }) => {
    await expect(canvas.getByText(/You left this league on 11 Dec 2026\. Results already played stay\./)).toBeVisible();
  },
};

/** Before the start there's no ladder to leave: nothing shows (a Manager takes a Player out, #159). */
export const NotStarted: Story = {
  args: { league: { ...running, stage: null, place: null } },
  play: async ({ canvas }) => {
    await expect(canvas.queryByRole("button", { name: "Ask to leave" })).toBeNull();
  },
};
