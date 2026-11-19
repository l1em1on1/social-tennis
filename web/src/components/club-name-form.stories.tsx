import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { ClubNameForm } from "./club-name-form";

const meta = {
  title: "Forms/ClubNameForm",
  component: ClubNameForm,
  args: { id: "club-name", submitLabel: "Create club" },
} satisfies Meta<typeof ClubNameForm>;
export default meta;
type Story = StoryObj<typeof meta>;

export const Create: Story = {};

export const Rename: Story = {
  args: { submitLabel: "Save name", club: { id: "c1", name: "Riverside Tennis Club" } },
};

/** From the press until the server answers. */
export const Saving: Story = {
  args: Rename.args,
  play: async ({ canvas, userEvent }) => {
    await userEvent.click(canvas.getByRole("button", { name: "Save name" }));
    await canvas.findByRole("button", { name: "Saving…" });
  },
};
