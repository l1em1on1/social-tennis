import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { expect } from "storybook/test";
import { IconButton, type IconButtonTone } from "./icon-button";
import { Icon, type IconName } from "./icons";

const meta = {
  title: "UI/IconButton",
  component: IconButton,
  args: { icon: "trash", label: "Remove Anna Smith" },
} satisfies Meta<typeof IconButton>;
export default meta;
type Story = StoryObj<typeof meta>;

const icons: IconName[] = [
  "trash",
  "check",
  "cross",
  "more",
  "up",
  "down",
  "next",
  "pencil",
  "leave",
  "personMinus",
  "personPlus",
  "unlink",
];
const tones: { tone: IconButtonTone; icon: IconName; label: string }[] = [
  { tone: "neutral", icon: "more", label: "Actions for anna@example.com" },
  { tone: "accent", icon: "check", label: "Approve Anna Smith" },
  { tone: "danger", icon: "trash", label: "Remove Anna Smith" },
];

/** Every icon, as the app draws them. */
export const Icons: Story = {
  render: () => (
    <div className="flex flex-wrap gap-4">
      {icons.map((name) => (
        <span key={name} className="flex flex-col items-center gap-1 text-sm">
          <Icon name={name} />
          {name}
        </span>
      ))}
    </div>
  ),
};

/** Each tone on a row's line: quiet, approve, remove. 44px, and named by its label alone. */
export const Tones: Story = {
  render: () => (
    <div className="flex gap-2">
      {tones.map(({ tone, icon, label }) => (
        <IconButton key={tone} tone={tone} icon={icon} label={label} />
      ))}
    </div>
  ),
  play: async ({ canvas }) => {
    const remove = canvas.getByRole("button", { name: "Remove Anna Smith" });
    await expect(remove.getBoundingClientRect().height).toBeGreaterThanOrEqual(44);
    await expect(remove.getBoundingClientRect().width).toBeGreaterThanOrEqual(44);
  },
};

/** With a word under the icon (#255): as high as the others, and narrower than the pill it replaces. */
export const WithAWord: Story = {
  render: () => (
    <div className="flex gap-2">
      <IconButton icon="personMinus" word="Leave out" label="Leave out Anna Smith" />
      <IconButton icon="pencil" word="Rename" label="Rename Winter Singles" />
      <IconButton icon="trash" word="Remove" tone="danger" label="Remove Anna Smith" />
    </div>
  ),
  play: async ({ canvas }) => {
    for (const button of canvas.getAllByRole("button")) {
      const { width, height } = button.getBoundingClientRect();
      await expect(height).toBe(44);
      await expect(width).toBeGreaterThanOrEqual(44);
      await expect(width).toBeLessThan(80);
    }
    await expect(canvas.getByRole("button", { name: "Leave out Anna Smith" })).toBeVisible();
  },
};

export const Disabled: Story = { args: { disabled: true } };
