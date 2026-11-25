import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { expect } from "storybook/test";
import { Badge } from "./badge";
import { Card } from "./card";

const meta = {
  title: "UI/Card",
  component: Card,
  args: { children: <p className="text-sm text-fg-muted">Riverside Tennis Club · 24 members</p> },
} satisfies Meta<typeof Card>;
export default meta;
type Story = StoryObj<typeof meta>;

/** The title is in the display face, which a story only gets if the font variable resolves at the element (#230 review). */
export const WithTitle: Story = {
  args: { title: "Clubs" },
  play: async ({ canvas }) => {
    await expect(getComputedStyle(canvas.getByRole("heading", { name: "Clubs" })).fontFamily).toMatch(/^['"]?_*Big[ _]Shoulders/);
  },
};

export const WithTitleAndAside: Story = {
  args: { title: "Join requests", aside: <Badge tone="warning">2 waiting</Badge> },
};

export const Plain: Story = {};
