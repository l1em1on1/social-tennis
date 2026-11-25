import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { expect } from "storybook/test";
import { GamesToSettle } from "./games-to-settle";
import { Card } from "./ui/card";

const meta = {
  title: "Manage/GamesToSettle",
  component: GamesToSettle,
  args: {
    games: [
      { id: "a1", division: 1, label: "Nia Brooks v Ella Grant" },
      { id: "a2", division: 2, label: "Maximiliana Featherstonehaugh v Ben Whitfield" },
    ],
  },
  render: (args) => (
    <Card title="To do before the Stage closes">
      <GamesToSettle {...args} />
    </Card>
  ),
} satisfies Meta<typeof GamesToSettle>;
export default meta;
type Story = StoryObj<typeof meta>;

/** Each row is one link, 44px high, read out whole. */
export const Rows: Story = {
  play: async ({ canvas }) => {
    const row = canvas.getByRole("link", { name: "Division 1 Nia Brooks v Ella Grant Settle" });
    await expect(row.getBoundingClientRect().height).toBeGreaterThanOrEqual(44);
  },
};
