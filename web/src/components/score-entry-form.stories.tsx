import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { expect, within } from "storybook/test";
import { entryOf } from "@/lib/score";
import { ScoreEntryForm } from "./score-entry-form";

const oneSetOf6 = { gamesPerSet: 6, setsPerGame: 1, matchTiebreakDecider: false };
const oneSetOf9 = { gamesPerSet: 9, setsPerGame: 1, matchTiebreakDecider: false };
const bestOf3 = { gamesPerSet: 6, setsPerGame: 3, matchTiebreakDecider: false };
const bestOf3MatchTiebreak = { gamesPerSet: 6, setsPerGame: 3, matchTiebreakDecider: true };

const set = (first: number, second: number, firstPoints = 0, secondPoints = 0) => ({ first, second, firstPoints, secondPoints });

const meta = {
  title: "Forms/ScoreEntryForm",
  component: ScoreEntryForm,
  args: {
    gameId: "00000000-0000-4000-8000-000000000009",
    playerId: "00000000-0000-4000-8000-000000000001",
    sides: ["Ben Carter", "Dev Shah"],
    format: oneSetOf6,
    stageStart: "2026-11-01",
    today: "2026-11-22",
  },
} satisfies Meta<typeof ScoreEntryForm>;
export default meta;
type Story = StoryObj<typeof meta>;

type Canvas = Parameters<NonNullable<Story["play"]>>[0]["canvas"];

const verdict = (canvas: Canvas) => canvas.getByRole("status");
const save = (canvas: Canvas) => canvas.getByRole("button", { name: "Save score" });
const more = (canvas: Canvas, group: string) =>
  within(canvas.getByRole("group", { name: group })).getByRole("button", { name: "One more" });

/** Nothing entered yet: one set of 6, played today, and nothing to save. */
export const Empty: Story = {
  play: async ({ canvas }) => {
    await expect(canvas.getByLabelText("Date played")).toHaveValue("2026-11-22");
    await expect(verdict(canvas)).toHaveTextContent("A set ends at 6 games, or 6–5 on a tiebreak.");
    await expect(save(canvas)).toBeDisabled();
  },
};

/** Tapping a set to 6–4 finishes it, and the winner is named. */
export const OneSet: Story = {
  args: { initial: { sets: [set(5, 4)] } },
  play: async ({ canvas, userEvent }) => {
    await userEvent.click(more(canvas, "Set, Ben Carter"));
    await expect(canvas.getByRole("region", { name: "Set" })).toHaveTextContent("6–4");
    await expect(verdict(canvas)).toHaveTextContent("Ben Carter wins");
    await expect(save(canvas)).toBeEnabled();
  },
};

/** At 5–5 the tiebreak points appear; a finished tiebreak makes the set 6–5, shown with the loser's points. */
export const TiebreakAtFiveAll: Story = {
  args: { initial: { sets: [set(5, 5, 7, 6)] } },
  play: async ({ canvas, userEvent }) => {
    await expect(canvas.getByText("Tiebreak points")).toBeVisible();
    await expect(verdict(canvas)).toHaveTextContent("A tiebreak needs a 2-point lead, like 7–5 or 8–6.");
    await userEvent.click(more(canvas, "Set tiebreak, Ben Carter"));
    await expect(canvas.getByLabelText("Score so far")).toHaveTextContent("6–5(6)");
    await expect(verdict(canvas)).toHaveTextContent("Ben Carter wins");
  },
};

/** Sets of 9: the tiebreak is at 8–8. */
export const SetOfNine: Story = {
  args: { format: oneSetOf9, initial: { sets: [set(8, 8, 3, 7)] } },
  play: async ({ canvas }) => {
    await expect(canvas.getByLabelText("Score so far")).toHaveTextContent("8–9(3)");
    await expect(verdict(canvas)).toHaveTextContent("Dev Shah wins");
  },
};

/** Best of three: the third set is held back until one set all. */
export const BestOfThreeStraightSets: Story = {
  args: { format: bestOf3, initial: { sets: [set(6, 4), set(6, 3)] } },
  play: async ({ canvas }) => {
    await expect(canvas.getByRole("region", { name: "Set 3" })).toHaveTextContent("Only at one set all");
    await expect(verdict(canvas)).toHaveTextContent("Ben Carter wins");
  },
};

/** At one set all, the decider is a first-to-10 match tiebreak where the League plays one. */
export const MatchTiebreak: Story = {
  args: { format: bestOf3MatchTiebreak, initial: { sets: [set(6, 4), set(3, 6), set(9, 7)] } },
  play: async ({ canvas, userEvent }) => {
    await expect(canvas.getByRole("region", { name: "Match tiebreak" })).toBeVisible();
    await expect(verdict(canvas)).toHaveTextContent("A match tiebreak is first to 10 points.");
    await userEvent.click(more(canvas, "Match tiebreak, Ben Carter"));
    await expect(canvas.getByLabelText("Score so far")).toHaveTextContent("6–4 3–6 [10–7]");
    await expect(save(canvas)).toBeEnabled();
  },
};

/** A Game that stopped early keeps its sets as they stood, and needs its winner picked. */
export const Retirement: Story = {
  args: { format: bestOf3, initial: { sets: [set(6, 4), set(2, 1)] } },
  play: async ({ canvas, userEvent }) => {
    await expect(verdict(canvas)).toHaveTextContent("A set ends at 6 games, or 6–5 on a tiebreak.");
    await userEvent.click(canvas.getByRole("switch", { name: /Stopped early/ }));
    await expect(verdict(canvas)).toHaveTextContent("Pick who won the game that stopped early.");
    await expect(save(canvas)).toBeDisabled();
    await userEvent.click(canvas.getByRole("radio", { name: "Dev Shah" }));
    await expect(verdict(canvas)).toHaveTextContent("Dev Shah wins (retirement)");
    await expect(save(canvas)).toBeEnabled();
  },
};

/** Stepping up to the final 6–5 asks for the tiebreak's points there too (#212 review): no need to go back to 5–5. */
export const TiebreakEnteredAsSixFive: Story = {
  args: { initial: { sets: [set(6, 4)] } },
  play: async ({ canvas, userEvent }) => {
    await userEvent.click(more(canvas, "Set, Dev Shah"));
    await expect(within(canvas.getByRole("region", { name: "Set" })).getByText(/won on a tiebreak at 5–5: enter its points/)).toBeVisible();
    for (let point = 0; point < 7; point++) await userEvent.click(more(canvas, "Set tiebreak, Ben Carter"));
    for (let point = 0; point < 4; point++) await userEvent.click(more(canvas, "Set tiebreak, Dev Shah"));
    await expect(canvas.getByLabelText("Score so far")).toHaveTextContent("6–5(4)");
    await expect(verdict(canvas)).toHaveTextContent("Ben Carter wins");
    await expect(save(canvas)).toBeEnabled();
  },
};

/** 6–6 can't happen in sets of 6: the set says so in red, and the steppers stop at 6. */
export const Impossible: Story = {
  args: { initial: { sets: [set(6, 6)] } },
  play: async ({ canvas }) => {
    await expect(within(canvas.getByRole("region", { name: "Set" })).getByText("6–6 can't happen: at 5–5 the set goes to a tiebreak.")).toBeVisible();
    await expect(more(canvas, "Set, Ben Carter")).toBeDisabled();
    await expect(save(canvas)).toBeDisabled();
  },
};

/** Correcting a Score: it opens as recorded, a 6–5 on a tiebreak included, and saves as a correction. */
export const Correcting: Story = {
  args: {
    mode: "correct",
    version: 7,
    format: bestOf3MatchTiebreak,
    initial: { sets: [entryOf({ first: 6, second: 5, firstTiebreak: 7, secondTiebreak: 4 }), set(3, 6), set(10, 7)], playedOn: "2026-11-12" },
  },
  play: async ({ canvas }) => {
    await expect(canvas.getByLabelText("Score so far")).toHaveTextContent("6–5(4) 3–6 [10–7]");
    await expect(canvas.getByLabelText("Date played")).toHaveValue("2026-11-12");
    await expect(verdict(canvas)).toHaveTextContent("Ben Carter wins");
    await expect(canvas.getByRole("button", { name: "Save correction" })).toBeEnabled();
  },
};

/** A Manager overriding a Retirement: it opens with its winner picked. */
export const Overriding: Story = {
  args: {
    mode: "override",
    playerId: undefined,
    version: 7,
    format: bestOf3,
    initial: { sets: [set(6, 4), set(2, 1)], retirementWinner: 2 },
  },
  play: async ({ canvas }) => {
    await expect(canvas.getByRole("switch", { name: /Stopped early/ })).toBeChecked();
    await expect(canvas.getByRole("radio", { name: "Dev Shah" })).toBeChecked();
    await expect(verdict(canvas)).toHaveTextContent("Dev Shah wins (retirement)");
    await expect(canvas.getByRole("button", { name: "Save override" })).toBeEnabled();
  },
};
