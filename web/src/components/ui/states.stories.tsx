import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { Button } from "./button";
import { EmptyState, ErrorState, Skeleton, SkeletonGroup } from "./states";

const meta = {
  title: "UI/States",
  component: EmptyState,
  args: { title: "No clubs yet", description: "Create the club so players can ask to join it." },
} satisfies Meta<typeof EmptyState>;
export default meta;
type Story = StoryObj<typeof meta>;

export const Empty: Story = {};

export const EmptyWithAction: Story = { args: { action: <Button>Create a club</Button> } };

export const ErrorMessage: Story = {
  render: () => (
    <ErrorState
      title="That search didn't work"
      description="Search with letters, numbers, spaces and punctuation."
      action={<Button variant="secondary">Try again</Button>}
    />
  ),
};

export const Loading: Story = {
  render: () => (
    <SkeletonGroup>
      <Skeleton className="h-6 w-2/3" />
      <Skeleton className="h-20" />
      <Skeleton className="h-20" />
    </SkeletonGroup>
  ),
};
