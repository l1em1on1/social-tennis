import { takeSnapshot as chromaticTakeSnapshot, test as base } from "@chromatic-com/playwright";
import type { Page, TestInfo } from "@playwright/test";

export { expect } from "@chromatic-com/playwright";

/**
 * Chromatic captures the whole page but draws a fixed element once, where it
 * sits on load, so on a page taller than the screen the bottom nav would cover
 * the content at the screen's bottom edge (#182). Just before each capture, a
 * page that doesn't fit the screen gets the nav in normal flow, drawn once at
 * its end. One that fits keeps it pinned, as a phone shows it. The nav lives
 * in the header (#225), so below the `md` breakpoint it is moved to the end of
 * the shell for the capture; from `md` up it is a top bar in the header, drawn
 * where it is. Returns a function that puts the nav back.
 */
async function navInFlow(page: Page): Promise<() => Promise<void>> {
  const added = await page.evaluate(() => {
    if (document.documentElement.scrollHeight <= window.innerHeight) return false;
    if (window.matchMedia("(width >= 48rem)").matches) return false;
    const nav = document.querySelector<HTMLElement>('nav[aria-label="Main"]');
    const shell = nav?.closest("header")?.parentElement;
    if (!nav || !shell) return false;
    const style = document.createElement("style");
    // The page's Content-Security-Policy admits only styles with its nonce
    // (ADR-0027), so this one takes the nonce Next.js gave the page's scripts.
    style.nonce = document.querySelector<HTMLElement>("[nonce]")?.nonce ?? "";
    style.id = "chromatic-nav-in-flow";
    style.textContent = 'nav[aria-label="Main"] { position: static !important; }';
    document.head.append(style);
    // A marker where the nav was, to put it back after the capture.
    nav.before(document.createComment("chromatic-nav-home"));
    shell.append(nav);
    return true;
  });
  return async () => {
    if (!added) return;
    await page.evaluate(() => {
      document.getElementById("chromatic-nav-in-flow")?.remove();
      const nav = document.querySelector('nav[aria-label="Main"]');
      const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_COMMENT);
      for (let node = walker.nextNode(); node; node = walker.nextNode()) {
        if (node.nodeValue === "chromatic-nav-home") {
          if (nav) node.parentNode?.insertBefore(nav, node);
          node.parentNode?.removeChild(node);
          break;
        }
      }
    });
  };
}

/**
 * `test` from `@chromatic-com/playwright`, whose `chromaticSnapshot` fixture
 * takes the automatic snapshot as it is torn down. This fixture depends on it,
 * so Playwright tears it down first: the nav is in flow by the time the
 * snapshot is taken.
 */
export const test = base.extend<{ chromaticNavInFlow: void }>({
  chromaticNavInFlow: [
    async ({ page, chromaticSnapshot, disableAutoSnapshot }, use) => {
      void chromaticSnapshot;
      await use();
      if (!disableAutoSnapshot) await navInFlow(page);
    },
    { auto: true },
  ],
});

/** `takeSnapshot` with the nav in flow for the capture, and pinned again after it. */
export async function takeSnapshot(page: Page, name: string, testInfo: TestInfo) {
  const restore = await navInFlow(page);
  await chromaticTakeSnapshot(page, name, testInfo);
  await restore();
}
