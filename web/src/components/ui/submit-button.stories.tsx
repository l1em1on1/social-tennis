import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { SubmitButton } from "./submit-button";

/** An action that never finishes, so the form stays pending. */
const never = () => new Promise<void>(() => {});

const meta = {
  title: "UI/SubmitButton",
  component: SubmitButton,
  args: { children: "Save name", pendingLabel: "Saving…" },
  decorators: [
    (Story) => (
      <form action={never}>
        <Story />
      </form>
    ),
  ],
} satisfies Meta<typeof SubmitButton>;
export default meta;
type Story = StoryObj<typeof meta>;

export const Idle: Story = {};

export const Danger: Story = { args: { variant: "danger", children: "Remove from club", pendingLabel: "Removing…" } };

/** From the press until the server answers: disabled, with a spinner and its pending label. */
export const Pending: Story = {
  args: { fullWidth: true },
  play: async ({ canvas, userEvent }) => {
    await userEvent.click(canvas.getByRole("button", { name: "Save name" }));
    await canvas.findByRole("button", { name: "Saving…" });
  },
};
