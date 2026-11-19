import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import type { PlayerSummary, Viewer } from "@/lib/viewer";
import { PlayerSwitcher } from "./player-switcher";

const pat: PlayerSummary = { id: "p1", name: "Pat Parent", gender: "Female", level: 3, clubs: [] };
const alex: PlayerSummary = { id: "p2", name: "Alex Junior", gender: "Male", level: 1, clubs: [] };
const sam: PlayerSummary = { id: "p3", name: "Sam Junior", gender: null, level: null, clubs: [] };

const viewer = (players: PlayerSummary[], acting: PlayerSummary): Viewer => ({
  userId: "u1",
  email: "pat@example.com",
  roles: [],
  players,
  actingPlayer: acting,
});

const meta = {
  title: "Shell/PlayerSwitcher",
  component: PlayerSwitcher,
  args: { viewer: viewer([pat, alex, sam], alex) },
  decorators: [
    (Story) => (
      <div className="flex justify-end">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof PlayerSwitcher>;
export default meta;
type Story = StoryObj<typeof meta>;

/** One Player: who you are playing as, with nothing to switch. */
export const OnePlayer: Story = { args: { viewer: viewer([pat], pat) } };

/** Several Players held by one login: a parent and their children. */
export const SeveralPlayers: Story = {};

export const SwitchOpen: Story = {
  play: async ({ canvas, userEvent }) => {
    await userEvent.click(canvas.getByRole("button", { name: /Playing as Alex Junior/ }));
  },
};
