import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { expect, within } from "storybook/test";
import { ScoreHistory } from "./score-history";

const set = (first: number, second: number, matchTiebreak = false) => ({ first, second, firstTiebreak: null, secondTiebreak: null, matchTiebreak });

const entered = {
  kind: "Entered" as const,
  by: "Dev Shah",
  at: "2026-11-12T21:40:00Z",
  before: null,
  after: { playedOn: "2026-11-12", sets: [set(6, 4), set(3, 6), set(7, 10, true)], winner: 2, retired: false },
};
const corrected = {
  kind: "Corrected" as const,
  by: "Ben Carter",
  at: "2026-11-13T08:12:00Z",
  before: entered.after,
  after: { playedOn: "2026-11-12", sets: [set(6, 4), set(3, 6), set(10, 7, true)], winner: 1, retired: false },
};
const overridden = {
  kind: "Overridden" as const,
  by: "Priya Nair",
  at: "2026-12-02T10:05:00Z",
  before: corrected.after,
  after: { playedOn: "2026-11-11", sets: [set(6, 4), set(2, 1)], winner: 1, retired: true },
};

const meta = {
  title: "Match/ScoreHistory",
  component: ScoreHistory,
  args: { history: [corrected, entered], sides: ["Ben Carter", "Dev Shah"], timeZone: "Europe/London" },
} satisfies Meta<typeof ScoreHistory>;
export default meta;
type Story = StoryObj<typeof meta>;

/** Entered, then corrected by the other side: the latest first, from what to what, at the Club's time. */
export const Corrected: Story = {
  play: async ({ canvas }) => {
    const items = within(canvas.getByRole("list", { name: "Changes to the score" })).getAllByRole("listitem");
    await expect(items[0]).toHaveTextContent("Ben Carter corrected it · 13 Nov, 08:12");
    await expect(items[0]).toHaveTextContent("6–4 3–6 [7–10] · Dev Shah won → to 6–4 3–6 [10–7] · Ben Carter won");
    await expect(items[1]).toHaveTextContent("Dev Shah entered it · 12 Nov, 21:40");
  },
};

/** A Manager's override, after the month closed, to a Retirement on another day: the day shows because it moved. */
export const Overridden: Story = {
  args: { history: [overridden, corrected, entered] },
  play: async ({ canvas }) => {
    const first = within(canvas.getByRole("list", { name: "Changes to the score" })).getAllByRole("listitem")[0];
    await expect(first).toHaveTextContent("Priya Nair (manager) overrode it · 2 Dec, 10:05");
    await expect(first).toHaveTextContent("6–4 2–1 · ret., Ben Carter won · played 11 Nov 2026");
  },
};

/** A Manager holding no Player of their own. */
export const ByAManager: Story = {
  args: { history: [{ ...overridden, by: null }] },
  play: async ({ canvas }) => {
    await expect(canvas.getByRole("listitem")).toHaveTextContent("A manager overrode it");
  },
};

/** A Manager recorded a Game nobody scored (#10), then a Player in it corrected it. */
export const RecordedByAManager: Story = {
  args: {
    history: [
      { ...corrected, before: { ...entered.after } },
      { ...entered, kind: "Recorded" as const, by: "Priya Nair" },
    ],
  },
  play: async ({ canvas }) => {
    const items = within(canvas.getByRole("list", { name: "Changes to the score" })).getAllByRole("listitem");
    await expect(items[1]).toHaveTextContent("Priya Nair (manager) recorded it · 12 Nov, 21:40");
    await expect(items[0]).toHaveTextContent("Ben Carter corrected it");
  },
};
