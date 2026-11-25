import { expect, type Locator, type Page } from "@playwright/test";

/** The least space between two buttons, one above the other (#255). */
const APART = 16;

/**
 * No two buttons, one above the other, are closer than 16px (#255): on the page,
 * or in the sheet open over it. A button is what looks like one, with an edge or
 * a fill: an icon button in a list's row has neither, and keeps to its row.
 */
export async function expectButtonsApart(page: Page, where: string) {
  const sheet = page.locator("dialog[open]");
  const scope = (await sheet.count()) > 0 ? sheet : page.getByRole("main");
  const tooClose = await scope.locator("button, a[href]").evaluateAll((elements, apart) => {
    const buttons = elements
      .filter((element) => {
        const style = getComputedStyle(element);
        const filled = !/^rgba\(.*, 0\)$|^transparent$/.test(style.backgroundColor);
        return (filled || Number.parseFloat(style.borderTopWidth) > 0) && element.getClientRects().length > 0;
      })
      .map((element) => ({ name: element.getAttribute("aria-label") ?? element.textContent?.trim() ?? "", box: element.getBoundingClientRect() }));
    return buttons.flatMap((upper) =>
      buttons
        .filter(({ box }) => box.top >= upper.box.bottom && box.left < upper.box.right && box.right > upper.box.left)
        .map((lower) => ({ lower, gap: Math.round((lower.box.top - upper.box.bottom) * 10) / 10 }))
        .filter(({ gap }) => gap < apart)
        .map(({ lower, gap }) => `"${upper.name}" and "${lower.name}": ${gap}px`),
    );
  }, APART);
  expect(tooClose, `${where}: buttons closer than ${APART}px, one above the other`).toEqual([]);
}

/** Every word of the text keeps to one line: none is broken in the middle. */
export async function expectWordsWhole(text: Locator) {
  const broken = await text.evaluateAll((elements) =>
    elements.flatMap((element) => {
      const walker = document.createTreeWalker(element, NodeFilter.SHOW_TEXT);
      const found: string[] = [];
      for (let node = walker.nextNode(); node; node = walker.nextNode()) {
        for (const word of (node.textContent ?? "").matchAll(/\S+/g)) {
          const range = document.createRange();
          range.setStart(node, word.index);
          range.setEnd(node, word.index + word[0].length);
          if (new Set([...range.getClientRects()].map((rect) => Math.round(rect.top))).size > 1) found.push(word[0]);
        }
      }
      return found;
    }),
  );
  expect(broken, "words broken in the middle").toEqual([]);
}
