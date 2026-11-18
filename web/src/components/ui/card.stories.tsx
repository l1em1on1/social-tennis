import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { Badge } from "./badge";
import { Card } from "./card";

const meta = {
  title: "UI/Card",
  component: Card,
  args: { children: <p className="text-sm text-fg-muted">Riverside Tennis Club · 24 members</p> },
} satisfies Meta<typeof Card>;
export default meta;
type Story = StoryObj<typeof meta>;

export const WithTitle: Story = { args: { title: "Clubs" } };

export const WithTitleAndAside: Story = {
  args: { title: "Join requests", aside: <Badge tone="warning">2 waiting</Badge> },
};

export const Plain: Story = {};
