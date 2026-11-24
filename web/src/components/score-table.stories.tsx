import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { expect } from "storybook/test";
import { ScoreTable } from "./score-table";
import { Card } from "./ui/card";

const set = (first: number, second: number) => ({
  first,
  second,
  firstTiebreak: null,
  secondTiebreak: null,
  matchTiebreak: false,
});

const meta = {
  title: "Match/ScoreTable",
  component: ScoreTable,
  args: { sides: ["Jo Bennett", "Alex Morgan"], sets: [set(6, 3), set(4, 6), set(6, 2)], winner: 1 },
  decorators: [
    (Story) => (
      <Card title="Score">
        <Story />
      </Card>
    ),
  ],
} satisfies Meta<typeof ScoreTable>;
export default meta;
type Story = StoryObj<typeof meta>;

/** The winner's row in bold with a tick, name included; the loser's isn't. */
export const Won: Story = {
  play: async ({ canvas }) => {
    const weight = (name: RegExp) => getComputedStyle(canvas.getByRole("rowheader", { name })).fontWeight;
    await expect(weight(/Jo Bennett/)).toBe("700");
    await expect(weight(/Alex Morgan/)).toBe("400");
  },
};

/** A name that is one long word breaks inside it, so the table keeps to its card on a phone. */
export const LongName: Story = {
  args: { sides: ["OpponentWithAnUnusuallyLongUnbrokenName", "Alex Morgan"] },
  play: async ({ canvas }) => {
    await document.fonts.ready;
    const table = canvas.getByRole("table", { name: "Score, set by set" });
    await expect(table.scrollWidth).toBeLessThanOrEqual(table.parentElement!.clientWidth);
  },
};
