import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { TeamPairingForm } from "./team-pairing-form";

const meta = {
  title: "Forms/TeamPairingForm",
  component: TeamPairingForm,
  args: {
    leagueId: "00000000-0000-4000-8000-000000000001",
    mixed: false,
    players: [
      { playerId: "00000000-0000-4000-8000-0000000000a1", name: "Alex Morgan", gender: "Female" },
      { playerId: "00000000-0000-4000-8000-0000000000a2", name: "Jo Bennett", gender: "Male" },
      { playerId: "00000000-0000-4000-8000-0000000000a3", name: "Priya Shah", gender: "Female" },
      { playerId: "00000000-0000-4000-8000-0000000000a4", name: "Sam Morgan", gender: null },
    ],
  },
} satisfies Meta<typeof TeamPairingForm>;
export default meta;
type Story = StoryObj<typeof meta>;

export const Doubles: Story = {};

/** Mixed doubles: each Player's gender is shown, and two of the same gender get a warning, not a refusal. */
export const MixedSameGender: Story = {
  args: { mixed: true },
  play: async ({ canvas, userEvent }) => {
    await userEvent.selectOptions(canvas.getByLabelText("First player"), "00000000-0000-4000-8000-0000000000a1");
    await userEvent.selectOptions(canvas.getByLabelText("Second player"), "00000000-0000-4000-8000-0000000000a3");
    await canvas.findByText(/Both are the same gender/);
  },
};
