import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { Pager } from "./pager";

const meta = {
  title: "UI/Pager",
  component: Pager,
  args: { label: "users" },
} satisfies Meta<typeof Pager>;
export default meta;
type Story = StoryObj<typeof meta>;

export const FirstPage: Story = { args: { nextHref: "/admin?offset=50" } };

export const MiddlePage: Story = { args: { previousHref: "/admin", nextHref: "/admin?offset=100" } };

export const LastPage: Story = { args: { previousHref: "/admin?offset=250" } };
