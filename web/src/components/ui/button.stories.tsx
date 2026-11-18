import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { Button, ButtonLink, type ButtonVariant } from "./button";

const variants: ButtonVariant[] = ["primary", "secondary", "ghost", "danger"];

const meta = { title: "UI/Button", component: Button } satisfies Meta<typeof Button>;
export default meta;
type Story = StoryObj<typeof meta>;

export const Variants: Story = {
  render: () => (
    <div className="flex flex-wrap gap-2">
      {variants.map((variant) => (
        <Button key={variant} variant={variant}>
          {variant[0].toUpperCase() + variant.slice(1)}
        </Button>
      ))}
    </div>
  ),
};

export const Disabled: Story = {
  render: () => (
    <div className="flex flex-wrap gap-2">
      {variants.map((variant) => (
        <Button key={variant} variant={variant} disabled>
          {variant[0].toUpperCase() + variant.slice(1)}
        </Button>
      ))}
    </div>
  ),
};

export const FullWidth: Story = { args: { fullWidth: true, children: "Email me a sign-in link" } };

/** A link that looks like a button: navigation stays a real <a>. */
export const Links: Story = {
  render: () => (
    <div className="flex flex-wrap gap-2">
      <ButtonLink href="/leagues">Leagues</ButtonLink>
      <ButtonLink href="/profiles" variant="secondary">
        Profiles
      </ButtonLink>
      <ButtonLink href="/" variant="ghost">
        Home
      </ButtonLink>
    </div>
  ),
};
