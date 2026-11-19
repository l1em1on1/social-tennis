import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { PlayerForm } from "./player-form";

const meta = {
  title: "Forms/PlayerForm",
  component: PlayerForm,
  args: { id: "player", label: "Your name", submitLabel: "Save name", from: "/" },
} satisfies Meta<typeof PlayerForm>;
export default meta;
type Story = StoryObj<typeof meta>;

/** The first sign-in asks only for a name. */
export const FirstSignIn: Story = { args: { hint: "As it appears on the ladder." } };

/** Profiles adds a Player with gender and level. */
export const AddWithDetails: Story = {
  args: { id: "new-player", label: "Player's name", submitLabel: "Add player", from: "/profiles", details: true },
};

export const Edit: Story = {
  args: {
    id: "edit-player",
    label: "Name",
    submitLabel: "Save",
    from: "/profiles",
    details: true,
    player: { id: "p1", name: "Alex Junior", gender: "Male", level: 2 },
  },
};
