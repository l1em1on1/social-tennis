/** Joins class names, skipping falsy entries — enough for owned primitives without a dependency. */
export function cx(...classes: Array<string | false | null | undefined>): string {
  return classes.filter(Boolean).join(" ");
}
