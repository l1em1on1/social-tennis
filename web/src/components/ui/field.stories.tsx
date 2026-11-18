import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { Field, Input } from "./field";
import { Select } from "./select";

const meta = {
  title: "UI/Field",
  component: Field,
  args: { id: "name", label: "Name", children: (control) => <Input {...control} defaultValue="Anna Smith" /> },
} satisfies Meta<typeof Field>;
export default meta;
type Story = StoryObj<typeof meta>;

export const WithHint: Story = { args: { hint: "As it appears on the ladder." } };

export const WithError: Story = {
  args: {
    id: "email",
    label: "Email",
    error: "Enter a valid email address.",
    children: (control) => <Input {...control} type="email" defaultValue="anna@" />,
  },
};

export const Empty: Story = {
  args: {
    id: "email-empty",
    label: "Email address",
    children: (control) => <Input {...control} type="email" placeholder="you@example.com" />,
  },
};

export const Disabled: Story = {
  args: { id: "name-disabled", children: (control) => <Input {...control} defaultValue="Anna Smith" disabled /> },
};

export const WithSelect: Story = {
  args: {
    id: "format",
    label: "Score format",
    children: (control) => (
      <Select {...control} defaultValue="best-of-three">
        <option value="best-of-three">Best of three</option>
        <option value="pro-set">Pro set</option>
      </Select>
    ),
  },
};
