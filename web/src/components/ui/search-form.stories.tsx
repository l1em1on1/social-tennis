import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { Field } from "./field";
import { SearchForm } from "./search-form";
import { Select } from "./select";

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

/** With another field, as the Users list has for a Club (#245). */
export const WithAnotherField: Story = {
  args: {
    children: (
      <Field id="user-club" label="Club">
        {(control) => (
          <Select {...control} name="club" defaultValue="">
            <option value="">All clubs</option>
            <option value="1">Riverside Tennis Club</option>
          </Select>
        )}
      </Field>
    ),
  },
};

/** A long address must fit the box at 360px (#167). */
export const WithSearch: Story = { args: { defaultValue: "search-admin@example.com" } };
