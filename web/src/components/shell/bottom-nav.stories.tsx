import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { allModes } from "../../../.storybook/modes";
import { BottomNav } from "./bottom-nav";

const meta = {
  title: "Shell/BottomNav",
  component: BottomNav,
  args: { keys: ["home", "leagues", "profiles"] },
  parameters: {
    nextjs: { navigation: { pathname: "/" } },
    // A phone's bar of tabs, and from md a top bar (#225).
    chromatic: { modes: { light: allModes.light, dark: allModes.dark, desktop: allModes.desktop } },
  },
  decorators: [
    // The nav is fixed to the bottom of the screen. A transform makes this box
    // its containing block, so it sits here and the snapshot has a height.
    (Story) => (
      <div className="relative h-20 transform-[translateZ(0)]">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof BottomNav>;
export default meta;
type Story = StoryObj<typeof meta>;

export const Player: Story = {};

export const Manager: Story = {
  args: { keys: ["home", "leagues", "profiles", "manage"] },
  parameters: { nextjs: { navigation: { pathname: "/manage/leagues/1" } } },
};

export const Admin: Story = {
  args: { keys: ["home", "leagues", "profiles", "manage", "admin"] },
  parameters: { nextjs: { navigation: { pathname: "/admin" } } },
};
