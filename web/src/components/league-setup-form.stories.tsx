import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { LeagueSetupForm } from "./league-setup-form";

const meta = {
  title: "Forms/LeagueSetupForm",
  component: LeagueSetupForm,
  // A fixed "today", so the date defaults don't change the snapshot.
  args: { clubId: "00000000-0000-4000-8000-000000000001", today: "2026-11-19" },
} satisfies Meta<typeof LeagueSetupForm>;
export default meta;
type Story = StoryObj<typeof meta>;

export const Empty: Story = {};

/** The API refused it: the messages sit next to the fields they name, with a summary on top. */
export const Refused: Story = {
  play: async ({ canvas, userEvent }) => {
    await userEvent.type(canvas.getByLabelText("Name"), "Winter Ladder");
    // The browser's own check needs every required field before the action runs.
    await userEvent.type(canvas.getByLabelText("Ends"), "2027-03-31");
    await userEvent.click(canvas.getByRole("button", { name: "Create League" }));
    await canvas.findByText("The League wasn't created");
  },
};
