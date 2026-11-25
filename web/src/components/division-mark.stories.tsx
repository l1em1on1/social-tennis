import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { expect } from "storybook/test";
import { Card } from "./ui/card";
import { DivisionMark } from "./division-mark";

const meta = { title: "Ladder/DivisionMark", component: DivisionMark, args: { division: 1 } } satisfies Meta<typeof DivisionMark>;
export default meta;
type Story = StoryObj<typeof meta>;

/** Beside names, on a card; two figures widen the ring. */
export const BesideNames: Story = {
  render: () => (
    <Card title="Teams">
      <ul className="flex flex-col gap-2">
        {[1, 2, 12].map((division) => (
          <li key={division} className="flex items-center gap-2">
            <DivisionMark division={division} /> Priya Shah &amp; Tom Okafor
          </li>
        ))}
      </ul>
    </Card>
  ),
  play: async ({ canvas }) => {
    const [first] = canvas.getAllByRole("listitem");
    await expect(first).toHaveTextContent("Division 1 Priya Shah & Tom Okafor");
  },
};
