import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { SearchForm } from "./search-form";

const meta = {
  title: "UI/SearchForm",
  component: SearchForm,
  args: {
    action: "/admin",
    id: "user-search",
    label: "Find a user",
    hint: "Part of an email address or a player's name.",
  },
} satisfies Meta<typeof SearchForm>;
export default meta;
type Story = StoryObj<typeof meta>;

export const Empty: Story = {};

/** A long address must fit the box at 360px (#167). */
export const WithSearch: Story = { args: { defaultValue: "search-admin@example.com" } };
