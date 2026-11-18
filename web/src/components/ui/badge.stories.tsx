import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { ResultStatusBadge, resultStatuses } from "../result-status-badge";
import { Badge, type BadgeTone } from "./badge";

const tones: BadgeTone[] = ["neutral", "accent", "success", "warning", "danger", "info"];

const meta = { title: "UI/Badge", component: Badge } satisfies Meta<typeof Badge>;
export default meta;
type Story = StoryObj<typeof meta>;

export const Tones: Story = {
  args: { children: "" },
  render: () => (
    <div className="flex flex-wrap gap-2">
      {tones.map((tone) => (
        <Badge key={tone} tone={tone}>
          {tone}
        </Badge>
      ))}
    </div>
  ),
};

/** Where a Game's result stands (CONTEXT.md: Game, Score, Retirement, Walkover, Void). */
export const ResultStatuses: Story = {
  args: { children: "" },
  render: () => (
    <div className="flex flex-wrap gap-2">
      {resultStatuses.map((status) => (
        <ResultStatusBadge key={status} status={status} />
      ))}
    </div>
  ),
};
