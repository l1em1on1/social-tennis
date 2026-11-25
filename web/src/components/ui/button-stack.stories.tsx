import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { expect } from "storybook/test";
import { Button } from "./button";
import { ButtonRow, ButtonStack } from "./button-stack";

const meta = { title: "UI/ButtonStack", component: ButtonStack, args: { children: null } } satisfies Meta<typeof ButtonStack>;
export default meta;
type Story = StoryObj<typeof meta>;

/** 16px between two buttons, one above the other. */
export const Stacked: Story = {
  render: () => (
    <ButtonStack>
      <Button variant="secondary" fullWidth>
        Record the score (manager)
      </Button>
      <Button variant="secondary" fullWidth>
        Mark not played (manager)
      </Button>
    </ButtonStack>
  ),
  play: async ({ canvas }) => {
    const [first, second] = canvas.getAllByRole("button").map((button) => button.getBoundingClientRect());
    await expect(second.top - first.bottom).toBe(16);
  },
};

/** Side by side while they fit, and 16px apart once they wrap. */
export const Wrapped: Story = {
  render: () => (
    <div className="w-56">
      <ButtonRow>
        <Button variant="secondary">Reset the link</Button>
        <Button variant="secondary">Turn it off</Button>
      </ButtonRow>
    </div>
  ),
  play: async ({ canvas }) => {
    const [first, second] = canvas.getAllByRole("button").map((button) => button.getBoundingClientRect());
    await expect(second.top - first.bottom).toBe(16);
  },
};
