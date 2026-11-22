import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { expect, within } from "storybook/test";
import { HeldUpBanner } from "./held-up-banner";

const meta = {
  title: "Ladder/HeldUpBanner",
  component: HeldUpBanner,
  args: {
    stageNumber: 1,
    endDate: "2026-11-30",
    unscored: ["Josh Lee v Ravi Rao", "Dev Shah v Liam Moss"],
    ties: [{ division: 2, names: ["Omar Ali", "Tom Reid"] }],
  },
} satisfies Meta<typeof HeldUpBanner>;
export default meta;
type Story = StoryObj<typeof meta>;

/** Past its deadline with Games unscored and a tie: each is named. */
export const WaitingOnTheManager: Story = {
  play: async ({ canvas }) => {
    const banner = canvas.getByRole("region", { name: "Stage held up" });
    await expect(banner).toHaveTextContent("Stage 1 ended on 30 Nov 2026 and is held up");
    await expect(within(banner).getAllByRole("listitem").map((item) => item.textContent)).toEqual([
      "No score yet: Josh Lee v Ravi Rao",
      "No score yet: Dev Shah v Liam Moss",
      "A tie in Division 2: Omar Ali, Tom Reid",
    ]);
  },
};

/** Everything settled: it only waits for the Manager to close it. */
export const ReadyToClose: Story = {
  args: { unscored: [], ties: [] },
  play: async ({ canvas }) => {
    await expect(canvas.getByRole("region", { name: "Stage held up" })).toHaveTextContent("Everything is settled: the manager closes it next.");
  },
};
