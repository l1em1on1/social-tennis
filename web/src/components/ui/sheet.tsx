"use client";

import { useId, useRef, type ReactNode } from "react";
import { cx } from "@/lib/cx";
import { buttonClasses, type ButtonVariant } from "./button";
import { IconButtonContent, iconButtonClasses, type IconButtonTone } from "./icon-button";
import type { IconName } from "./icons";

type SheetProps = {
  title: string;
  triggerClassName?: string;
  children: ReactNode;
} & (
  | {
      /** Content of the button that opens the sheet. */
      trigger: ReactNode;
      triggerVariant?: ButtonVariant;
      triggerIcon?: never;
    }
  | {
      /** An icon button opens it instead (#224), named by `trigger`: "Remove Anna Smith". */
      triggerIcon: IconName;
      trigger: string;
      /** A word under the icon (#255); `trigger` starts with it. */
      triggerWord?: string;
      triggerTone?: IconButtonTone;
    }
);

/**
 * Bottom sheet on phones, centred dialog on wider screens. Built on the native
 * `<dialog>` + `showModal()`: the browser supplies the focus trap, Escape to
 * close, inert background, and top-layer stacking — the parts ADR-0014 says
 * are hard to get right by hand.
 */
export function Sheet(props: SheetProps) {
  const { title, trigger, triggerClassName, children } = props;
  const ref = useRef<HTMLDialogElement>(null);
  const titleId = useId();
  const open = () => ref.current?.showModal();

  return (
    <>
      {props.triggerIcon ? (
        <button
          type="button"
          aria-haspopup="dialog"
          aria-label={props.trigger}
          title={props.trigger}
          className={cx(iconButtonClasses(props.triggerTone, Boolean(props.triggerWord)), triggerClassName)}
          onClick={open}
        >
          <IconButtonContent icon={props.triggerIcon} word={props.triggerWord} />
        </button>
      ) : (
        <button
          type="button"
          aria-haspopup="dialog"
          className={cx(buttonClasses(props.triggerVariant ?? "secondary"), triggerClassName)}
          onClick={open}
        >
          {trigger}
        </button>
      )}
      <dialog
        ref={ref}
        aria-labelledby={titleId}
        // Clicking the backdrop (the dialog element itself, outside its panel) closes it.
        onClick={(event) => {
          if (event.target === event.currentTarget) event.currentTarget.close();
        }}
        // A choice made inside the sheet (any form submitting, e.g. to a server
        // action) is the end of the interaction: close, and let the page update.
        onSubmit={() => ref.current?.close()}
        className={cx(
          "m-0 mt-auto w-full max-w-none rounded-t-3xl border border-border bg-surface p-0 text-fg",
          "sm:m-auto sm:max-w-md sm:rounded-3xl",
          "backdrop:bg-fg/40",
        )}
      >
        <div className="flex flex-col gap-4 p-5 pb-8">
          <header className="flex items-center justify-between gap-3">
            <h2 id={titleId} className="text-lg font-semibold">
              {title}
            </h2>
            <form method="dialog">
              <button type="submit" className={buttonClasses("ghost")} aria-label="Close">
                <span aria-hidden="true" className="text-xl leading-none">
                  ×
                </span>
              </button>
            </form>
          </header>
          {children}
        </div>
      </dialog>
    </>
  );
}
