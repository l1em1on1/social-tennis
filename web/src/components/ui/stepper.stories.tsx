import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { useState } from "react";
import { expect } from "storybook/test";
import { Stepper } from "./stepper";

function Counter({ start, max }: { start: number; max: number }) {
  const [value, setValue] = useState(start);
  return <Stepper label="Set 1, Una" value={value} max={max} onChange={setValue} />;
}

const meta = {
  title: "UI/Stepper",
  component: Counter,
  args: { start: 0, max: 6 },
} satisfies Meta<typeof Counter>;
export default meta;
type Story = StoryObj<typeof meta>;

/** At 0, "One less" is off. */
export const AtZero: Story = {
  play: async ({ canvas }) => {
    await expect(canvas.getByRole("button", { name: "One less" })).toBeDisabled();
  },
};

/** Up to the most a Set can hold, then "One more" is off. */
export const UpToTheTop: Story = {
  args: { start: 5 },
  play: async ({ canvas, userEvent }) => {
    const group = canvas.getByRole("group", { name: "Set 1, Una" });
    await userEvent.click(canvas.getByRole("button", { name: "One more" }));
    await expect(group).toHaveTextContent("6");
    await expect(canvas.getByRole("button", { name: "One more" })).toBeDisabled();
  },
};
