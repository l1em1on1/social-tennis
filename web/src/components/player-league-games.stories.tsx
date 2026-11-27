import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { expect, within } from "storybook/test";
import { HeldUpNotice, PlayerLeagueGames } from "./player-league-games";

const set = (first: number, second: number) => ({ first, second, firstTiebreak: null, secondTiebreak: null, matchTiebreak: false });
const game = (gameId: string, opponents: string[], extra: Record<string, unknown> = {}) => ({
  gameId,
  yourSide: 1,
  partners: [],
  opponents,
  score: null,
  notPlayed: false,
  date: null,
  court: null,
  ...extra,
});

const meta = {
  title: "Home/PlayerLeagueGames",
  component: PlayerLeagueGames,
  args: {
    league: {
      leagueId: "00000000-0000-4000-8000-000000000001",
      leagueName: "Winter Singles",
      clubName: "Riverside Tennis Club",
      stage: { number: 2, startDate: "2026-11-05", endDate: "2026-12-04" },
      division: 3,
      heldUp: false,
      unscoredGames: 4,
      openTies: 0,
      games: [
        game("g1", ["Dev Shah"]),
        // Won from Side 2: shown with the Player's games first.
        game("g2", ["Ravi Rao"], { yourSide: 2, score: { winner: 2, retired: false, sets: [set(4, 6), set(3, 6)] } }),
        game("g3", ["Liam Moss"], { notPlayed: true }),
      ],
    },
  },
} satisfies Meta<typeof PlayerLeagueGames>;
export default meta;
type Story = StoryObj<typeof meta>;

/** Mid-Stage: one Game to play, one won, one not played, and the deadline. */
export const MidStage: Story = {
  play: async ({ canvas }) => {
    const games = within(canvas.getByRole("list", { name: "Your games in Winter Singles" })).getAllByRole("listitem");
    await expect(games[0]).toHaveTextContent("v Dev ShahScore needed");
    await expect(games[1]).toHaveTextContent("v Ravi RaoWon 6–4 6–3Played");
    await expect(games[2]).toHaveTextContent("v Liam MossVoid");
    await expect(canvas.getByText(/play by/)).toHaveTextContent("Stage 2 · play by 4 Dec 2026");
    // The one Game owing a Score goes straight to its entry.
    await expect(canvas.getByRole("link", { name: "Enter score" })).toHaveAttribute("href", "/games/g1/score");
  },
};

/** Every Game scored: nothing to enter, just the ladder. */
export const AllScored: Story = {
  args: { league: { ...meta.args.league, games: [meta.args.league.games[1]] } },
  play: async ({ canvas }) => {
    await expect(canvas.queryByRole("link", { name: /Enter/ })).toBeNull();
    await expect(canvas.getByRole("link", { name: "Ladder" })).toBeVisible();
  },
};

/** Past the deadline with things unsettled: held up, and on what, above the card as on Home. */
export const HeldUp: Story = {
  args: { league: { ...meta.args.league, heldUp: true, unscoredGames: 2, openTies: 1 } },
  render: (args) => (
    <div className="flex flex-col gap-3">
      <HeldUpNotice league={args.league} />
      <PlayerLeagueGames league={args.league} />
    </div>
  ),
  play: async ({ canvas }) => {
    const notice = canvas.getByRole("region", { name: "Winter Singles: Stage held up" });
    await expect(notice).toHaveTextContent(
      "Winter Singles: Stage 2 ended on 4 Dec 2026 and is held up2 games have no score, and 1 tie decides who moves.",
    );
    await expect(within(notice).getByRole("link", { name: "See what's waiting" })).toHaveAttribute(
      "href",
      "/leagues/00000000-0000-4000-8000-000000000001",
    );
  },
};

/** Held up with nothing left to settle: the manager closes it next. */
export const HeldUpSettled: Story = {
  args: { league: { ...meta.args.league, heldUp: true, unscoredGames: 0, openTies: 0 } },
  render: (args) => <HeldUpNotice league={args.league} />,
  play: async ({ canvas }) => {
    await expect(canvas.getByRole("region")).toHaveTextContent("Everything is settled: your manager closes it next.");
  },
};

/** Doubles: the partner, and both opponents; two Games to score open the first's Match screen. */
export const Doubles: Story = {
  args: {
    league: {
      ...meta.args.league,
      leagueName: "Mixed Doubles",
      games: [
        game("g4", ["Cara Diaz", "Ed Fox"], { partners: ["Anna Price"] }),
        game("g5", ["Anna Price", "Ed Fox"], { partners: ["Cara Diaz"] }),
      ],
    },
  },
  play: async ({ canvas }) => {
    await expect(canvas.getAllByRole("listitem")[0]).toHaveTextContent("With Anna Price v Cara Diaz & Ed Fox");
    await expect(canvas.getByRole("link", { name: "Enter a score" })).toHaveAttribute("href", "/games/g4");
  },
};

/** In the League but left off this Stage's ladder. */
export const OffTheLadder: Story = {
  args: { league: { ...meta.args.league, division: null, games: [] } },
  play: async ({ canvas }) => {
    await expect(canvas.getByText(/not on the ladder this Stage/)).toBeVisible();
  },
};
