import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { expect } from "storybook/test";
import { allModes } from "../../../.storybook/modes";
import { Button } from "./button";
import { Sheet } from "./sheet";

/** A phone's screen: the open sheet is fixed to its bottom, outside the story's own height. */
const screen = { width: 360, height: 640 };

const meta = {
  title: "UI/Sheet",
  component: Sheet,
  args: {
    title: "Remove Anna Smith?",
    trigger: "Remove",
    children: (
      <>
        <p className="text-fg-muted">Anna Smith will no longer be a member of Riverside Tennis Club.</p>
        <Button variant="danger" fullWidth>
          Remove from club
        </Button>
      </>
    ),
  },
} satisfies Meta<typeof Sheet>;
export default meta;
type Story = StoryObj<typeof meta>;

export const Closed: Story = {};

/** Opened by its trigger: a modal <dialog> from the bottom of the screen. */
export const Open: Story = {
  // A whole screen, so Chromatic's snapshot reaches the sheet at the bottom.
  parameters: {
    chromatic: {
      modes: {
        light: { ...allModes.light, viewport: screen },
        dark: { ...allModes.dark, viewport: screen },
      },
    },
  },
  decorators: [
    (Story) => (
      <div className="min-h-[608px]">
        <Story />
      </div>
    ),
  ],
  play: async ({ canvas, userEvent }) => {
    await userEvent.click(canvas.getByRole("button", { name: "Remove" }));
  },
};

/** Opened from an icon button on a row's line (#224), named "Remove Anna Smith". */
export const IconTrigger: Story = {
  args: { triggerIcon: "trash", trigger: "Remove Anna Smith", triggerTone: "danger" },
  play: async ({ canvas }) => {
    await expect(canvas.getByRole("button", { name: "Remove Anna Smith" })).toHaveAttribute("aria-haspopup", "dialog");
  },
};
