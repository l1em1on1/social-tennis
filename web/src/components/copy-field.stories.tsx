import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { expect } from "storybook/test";
import { CopyField } from "./copy-field";

const meta = {
  title: "Manage/CopyField",
  component: CopyField,
  args: {
    id: "join-link-url",
    label: "Link to ask to join Riverside Tennis Club",
    hint: "Share it in the club's chat or by email. Whoever opens it can ask to join, and you approve them below.",
    value: "https://hitsandgiggles.example/join/FixedJoinCodeForSnapsh",
  },
} satisfies Meta<typeof CopyField>;
export default meta;
type Story = StoryObj<typeof meta>;

/** A Club's join link (#222), read-only so a long press selects it. */
export const JoinLink: Story = {
  play: async ({ canvas }) => {
    const field = canvas.getByLabelText("Link to ask to join Riverside Tennis Club");
    await expect(field).toHaveValue("https://hitsandgiggles.example/join/FixedJoinCodeForSnapsh");
    await expect(field).toHaveAttribute("readonly");
  },
};
