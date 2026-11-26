import type { Meta, StoryObj } from "@storybook/nextjs-vite";
import { expect } from "storybook/test";
import { Card } from "./card";
import { Combobox } from "./combobox";
import { Field } from "./field";

const meta = {
  title: "UI/Combobox",
  component: Combobox,
  args: {
    id: "partner",
    name: "partnerId",
    listLabel: "Players",
    placeholder: "Type a name…",
    options: [
      { value: "00000000-0000-4000-8000-0000000000a1", label: "Alex Morgan" },
      { value: "00000000-0000-4000-8000-0000000000a2", label: "Jo Bennett" },
      { value: "00000000-0000-4000-8000-0000000000a3", label: "Priya Shah" },
      { value: "00000000-0000-4000-8000-0000000000a4", label: "Sam Morgan" },
    ],
  },
  // In a Field, as it is used, and with room below, so the open list shows in the snapshot.
  render: (args) => (
    <form className="min-h-72">
      <Field id={args.id} label="Partner" hint="Type a name and pick from the list.">
        {(control) => <Combobox {...args} {...control} />}
      </Field>
    </form>
  ),
} satisfies Meta<typeof Combobox>;
export default meta;
type Story = StoryObj<typeof meta>;

export const Empty: Story = {};

/** Typing filters by any part of the name; the arrow keys move through what's left. */
export const Suggesting: Story = {
  play: async ({ canvas, userEvent }) => {
    await userEvent.type(canvas.getByRole("combobox", { name: "Partner" }), "morgan");
    await userEvent.keyboard("{ArrowDown}");
    const options = canvas.getAllByRole("option");
    await expect(options.map((option) => option.textContent)).toEqual(["Alex Morgan", "Sam Morgan"]);
    await expect(options[0]).toHaveAttribute("aria-selected", "true");
  },
};

/** Enter chooses the highlighted name; the form gets its id, not the text. */
export const Chosen: Story = {
  play: async ({ canvas, canvasElement, userEvent }) => {
    const box = canvas.getByRole("combobox", { name: "Partner" });
    await userEvent.type(box, "jo");
    await userEvent.keyboard("{ArrowDown}{Enter}");
    await expect(box).toHaveValue("Jo Bennett");
    await expect(box).toHaveAttribute("aria-expanded", "false");
    const form = canvasElement.querySelector("form")!;
    await expect(new FormData(form).get("partnerId")).toBe("00000000-0000-4000-8000-0000000000a2");
  },
};

/** Text that names nobody can't be submitted as if the box were empty. */
export const NoMatch: Story = {
  play: async ({ canvas, userEvent }) => {
    const box = canvas.getByRole("combobox", { name: "Partner" });
    await userEvent.type(box, "zed");
    await canvas.findByText("No one by that name.");
    await expect((box as HTMLInputElement).validity.valid).toBe(false);
  },
};

const many = ["Alex Morgan", "Jo Bennett", "Priya Shah", "Sam Morgan", "Nia Brooks", "Ella Grant", "Chris Lee", "Maya Ross"].map(
  (label, index) => ({ value: `00000000-0000-4000-8000-0000000000b${index}`, label }),
);

/**
 * On a card near its foot, as on the Leagues page's "You can join": the open
 * list runs past the card's edge, and every option in it can still be tapped
 * (#257 review). The arrow keys move through it without moving the card.
 */
export const OnACard: Story = {
  args: { options: many },
  render: (args) => (
    <div className="min-h-96">
      <Card title="Winter Doubles">
        <Field id={args.id} label="Partner" hint="Type a name and pick from the list.">
          {(control) => <Combobox {...args} {...control} />}
        </Field>
      </Card>
    </div>
  ),
  play: async ({ canvas, canvasElement, userEvent }) => {
    const card = canvasElement.querySelector("section")!;
    const top = card.querySelector("header")!.getBoundingClientRect().top;
    await userEvent.type(canvas.getByRole("combobox", { name: "Partner" }), "a");
    const list = canvas.getByRole("listbox").getBoundingClientRect();
    const options = canvas.getAllByRole("option");
    await expect(options.length).toBeGreaterThan(4);
    const hidden = options
      .filter((option) => option.getBoundingClientRect().bottom <= list.bottom)
      .filter((option) => {
        const box = option.getBoundingClientRect();
        const hit = document.elementFromPoint(box.left + box.width / 2, box.top + box.height / 2);
        return !hit || !option.contains(hit);
      })
      .map((option) => option.textContent);
    await expect(hidden).toEqual([]);
    for (let i = 0; i < options.length; i++) await userEvent.keyboard("{ArrowDown}");
    await expect(card.querySelector("header")!.getBoundingClientRect().top).toBe(top);
  },
};
