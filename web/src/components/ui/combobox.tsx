"use client";

import { useEffect, useId, useRef, useState, type KeyboardEvent } from "react";
import { cx } from "@/lib/cx";
import type { FieldControlProps } from "./field";
import { controlClasses } from "./field";

export interface ComboboxOption {
  value: string;
  label: string;
}

interface ComboboxProps extends FieldControlProps {
  /** The form field the chosen option's value is submitted as; empty when nothing is chosen. */
  name: string;
  options: ComboboxOption[];
  placeholder?: string;
  /** The suggestions list's accessible name, e.g. "Players". */
  listLabel?: string;
  /** Shown under the box when the typed text matches nothing. */
  noMatches?: string;
  /** Asked of the browser on submit when text is typed but nothing is chosen. */
  pickMessage?: string;
}

const matches = (option: ComboboxOption, text: string) => option.label.toLowerCase().includes(text.trim().toLowerCase());

/**
 * A text box that suggests options as you type, and submits the chosen
 * option's value, never the text: the ARIA combobox pattern, list
 * autocomplete with manual selection (WAI-ARIA APG). Focus stays in the box;
 * `aria-activedescendant` points at the highlighted option. Use inside a Field.
 *
 * Down and Up move through the suggestions (Alt+Down opens them, Alt+Up
 * closes them), Enter or a tap chooses one, and Escape closes the list, or
 * clears the box when it's already closed. Typed text that names no option is
 * refused on submit, so a half-typed name is never taken as "nothing chosen";
 * an exact name, if only one option has it, counts as choosing it.
 */
export function Combobox({
  id,
  name,
  options,
  placeholder,
  listLabel = "Suggestions",
  noMatches = "No one by that name.",
  pickMessage = "Pick a name from the list, or clear the box.",
  ...described
}: ComboboxProps) {
  const listId = `${useId()}-options`;
  const optionId = (index: number) => `${listId}-${index}`;
  const input = useRef<HTMLInputElement>(null);
  const [text, setText] = useState("");
  const [chosen, setChosen] = useState<ComboboxOption | null>(null);
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(-1);

  const shown = options.filter((option) => matches(option, text));
  const expanded = open && shown.length > 0;

  useEffect(() => {
    input.current?.setCustomValidity(text.trim() !== "" && !chosen ? pickMessage : "");
  }, [text, chosen, pickMessage]);

  useEffect(() => {
    if (expanded && active >= 0) {
      document.getElementById(`${listId}-${active}`)?.scrollIntoView({ block: "nearest" });
    }
  }, [expanded, active, listId]);

  function choose(option: ComboboxOption) {
    setChosen(option);
    setText(option.label);
    setOpen(false);
    setActive(-1);
  }

  function type(value: string) {
    setText(value);
    setOpen(true);
    setActive(-1);
    const exact = options.filter((option) => option.label.toLowerCase() === value.trim().toLowerCase());
    setChosen(exact.length === 1 ? exact[0] : null);
  }

  function onKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    switch (event.key) {
      case "ArrowDown":
        event.preventDefault();
        setOpen(true);
        if (!event.altKey && shown.length > 0) setActive((index) => Math.min(index + 1, shown.length - 1));
        break;
      case "ArrowUp":
        event.preventDefault();
        if (event.altKey) {
          setOpen(false);
          setActive(-1);
        } else if (shown.length > 0) {
          setOpen(true);
          setActive((index) => (index <= 0 ? shown.length - 1 : index - 1));
        }
        break;
      case "Enter":
        if (expanded && active >= 0) {
          event.preventDefault();
          choose(shown[active]);
        }
        break;
      case "Escape":
        if (expanded) {
          event.preventDefault();
          setOpen(false);
          setActive(-1);
        } else if (text !== "") {
          event.preventDefault();
          type("");
          setOpen(false);
        }
        break;
    }
  }

  return (
    <div className="relative">
      <input type="hidden" name={name} value={chosen?.value ?? ""} />
      <input
        ref={input}
        id={id}
        {...described}
        type="text"
        role="combobox"
        aria-autocomplete="list"
        aria-expanded={expanded}
        aria-controls={listId}
        aria-activedescendant={expanded && active >= 0 ? optionId(active) : undefined}
        autoComplete="off"
        spellCheck={false}
        placeholder={placeholder}
        value={text}
        onChange={(event) => type(event.target.value)}
        onClick={() => setOpen((wasOpen) => !wasOpen)}
        onKeyDown={onKeyDown}
        onBlur={() => {
          setOpen(false);
          setActive(-1);
        }}
        className={controlClasses}
      />
      <ul
        id={listId}
        role="listbox"
        aria-label={listLabel}
        hidden={!expanded}
        className="absolute inset-x-0 top-full z-20 mt-1 max-h-60 overflow-y-auto rounded-xl border border-border-strong bg-surface py-1 shadow-lg"
      >
        {shown.map((option, index) => (
          <li
            key={option.value}
            id={optionId(index)}
            role="option"
            aria-selected={index === active}
            // Keep focus in the box, so choosing doesn't blur it and close the list first.
            onPointerDown={(event) => event.preventDefault()}
            onClick={() => choose(option)}
            className={cx(
              "flex min-h-11 cursor-pointer items-center px-3 text-base",
              index === active ? "bg-accent-muted text-fg" : "hover:bg-surface-muted",
            )}
          >
            {option.label}
          </li>
        ))}
      </ul>
      <p className="text-sm text-fg-muted empty:hidden" aria-live="polite">
        {open && text.trim() !== "" && shown.length === 0 ? noMatches : null}
      </p>
    </div>
  );
}
