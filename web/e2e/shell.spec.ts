// Runs in the real project only (playwright.config.ts), which takes no snapshots: those come
// from the fixed data (screens.spec.ts, #234). `test` is e2e/support/chromatic.ts's all the same.
import { expect, test } from "./support/chromatic";
import type { Page } from "@playwright/test";
import { addAnotherPlayer, addFirstPlayer, logIn, personaEmail } from "./support/auth";
import { expectNoSeriousA11yViolations } from "./support/axe";
import { expectButtonsApart } from "./support/spacing";

const mainNav = (page: Page) => page.getByRole("navigation", { name: "Main" });

test("the shell fits a 360px screen with 44px tap targets", async ({ page }, testInfo) => {
  await logIn(page, testInfo, personaEmail(testInfo, "manager-admin"));

  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );
  expect(overflow, "horizontal scroll at 360px").toBeLessThanOrEqual(0);

  const links = mainNav(page).getByRole("link");
  await expect(links).toHaveCount(5);
  for (const link of await links.all()) {
    const box = await link.boundingBox();
    expect(box?.width).toBeGreaterThanOrEqual(44);
    expect(box?.height).toBeGreaterThanOrEqual(44);
  }
});

test.describe("on a desktop screen", () => {
  test.use({ viewport: { width: 1280, height: 800 }, isMobile: false, hasTouch: false });

  test("the navigation is a top bar in the header, with 44px targets and nothing wider than the screen", async ({
    page,
  }, testInfo) => {
    await logIn(page, testInfo, personaEmail(testInfo, "manager-admin"));

    const overflow = await page.evaluate(
      () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
    );
    expect(overflow, "horizontal scroll at 1280px").toBeLessThanOrEqual(0);

    // In the header, at the top: no bar of tabs at the bottom (#225).
    const nav = page.getByRole("banner").getByRole("navigation", { name: "Main" });
    await expect(nav.getByRole("link")).toHaveText(["Home", "Leagues", "Profiles", "Manage", "Admin"]);
    const header = (await page.getByRole("banner").boundingBox())!;
    const navBox = (await nav.boundingBox())!;
    expect(navBox.y + navBox.height).toBeLessThanOrEqual(header.y + header.height);
    for (const link of await nav.getByRole("link").all()) {
      const box = await link.boundingBox();
      expect(box?.height).toBeGreaterThanOrEqual(44);
    }
    // The content uses the width: wider than a phone's column.
    expect((await page.getByRole("main").boundingBox())!.width).toBeGreaterThan(700);
    await expectNoSeriousA11yViolations(page);
  });
});

test.describe("at the narrow end of md, a tablet held upright", () => {
  test.use({ viewport: { width: 768, height: 1024 }, isMobile: false, hasTouch: false });

  test("the header fits five entries and two Players with a long name", async ({ page }, testInfo) => {
    await logIn(page, testInfo, personaEmail(testInfo, "manager-admin"));
    await addFirstPlayer(page, "Bartholomew Featherstonehaugh");
    await mainNav(page).getByRole("link", { name: "Profiles" }).click();
    await addAnotherPlayer(page, "Bartholomew Featherstonehaugh Junior");
    await mainNav(page).getByRole("link", { name: "Home" }).click();
    await expect(page.getByRole("heading", { name: "Home", level: 1 })).toBeVisible();

    // Icons only here, each still named by its label (#229 review).
    await expect(mainNav(page).getByRole("link")).toHaveText(["Home", "Leagues", "Profiles", "Manage", "Admin"]);
    const overflow = await page.evaluate(
      () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
    );
    expect(overflow, "horizontal scroll at 768px").toBeLessThanOrEqual(0);
    for (const link of await mainNav(page).getByRole("link").all()) {
      const box = await link.boundingBox();
      expect(box?.width).toBeGreaterThanOrEqual(44);
      expect(box?.height).toBeGreaterThanOrEqual(44);
    }
  });
});

test("a Player without roles sees no Manage or Admin entry, and can't open them", async ({ page }, testInfo) => {
  await logIn(page, testInfo, personaEmail(testInfo, "player"));

  await expect(mainNav(page).getByRole("link")).toHaveText(["Home", "Leagues", "Profiles"]);
  // Asserted on the page, not the status: the page streams under a loading
  // boundary, so notFound() can arrive after a 200 has already been sent.
  for (const path of ["/manage", "/admin"]) {
    await page.goto(path);
    await expect(page.getByRole("heading", { name: "Page not found" })).toBeVisible();
  }
});

test("a Manager sees Manage; an Admin sees Manage and Admin", async ({ page }, testInfo) => {
  await logIn(page, testInfo, personaEmail(testInfo, "manager"));
  await expect(mainNav(page).getByRole("link")).toHaveText(["Home", "Leagues", "Profiles", "Manage"]);
  await mainNav(page).getByRole("link", { name: "Manage" }).click();
  await expect(page.getByRole("heading", { name: "Manage", level: 1 })).toBeVisible();
  await expect(mainNav(page).getByRole("link", { name: "Manage" })).toHaveAttribute("aria-current", "page");

  await page.context().clearCookies();
  await logIn(page, testInfo, personaEmail(testInfo, "admin"));
  // An Admin can do everything a Manager can.
  await expect(mainNav(page).getByRole("link")).toHaveText(["Home", "Leagues", "Profiles", "Manage", "Admin"]);
});

test("switching the acting Player survives navigation", async ({ page }, testInfo) => {
  await logIn(page, testInfo, personaEmail(testInfo, "parent"));
  await addFirstPlayer(page, "Pat Parent");
  await mainNav(page).getByRole("link", { name: "Profiles" }).click();
  await addAnotherPlayer(page, "Alex Junior");
  await addAnotherPlayer(page, "Sam Junior");
  const acting = page.getByTestId("acting-player");
  await expect(acting).toHaveText("Pat Parent");
  await expectButtonsApart(page, "Profiles");

  await page.getByRole("button", { name: /switch player/ }).click();
  const sheet = page.getByRole("dialog", { name: "Play as" });
  await expect(sheet).toBeVisible();
  await expectButtonsApart(page, "Play as");
  await expectNoSeriousA11yViolations(page);
  await sheet.getByRole("button", { name: "Alex Junior" }).click();
  await expect(acting).toHaveText("Alex Junior");

  await mainNav(page).getByRole("link", { name: "Leagues" }).click();
  await expect(page.getByRole("heading", { name: "Leagues", level: 1 })).toBeVisible();
  await expect(acting).toHaveText("Alex Junior");

  await page.reload();
  await expect(acting).toHaveText("Alex Junior");

  await mainNav(page).getByRole("link", { name: "Profiles" }).click();
  await expect(page.getByRole("listitem").filter({ hasText: "Alex Junior" })).toContainText("Playing as");
  await expectNoSeriousA11yViolations(page);
});

test("the pages are British English, and so is the browser the tests run: its time boxes are 24-hour (#275)", async ({ page }) => {
  await page.goto("/login");
  await expect(page.locator("html")).toHaveAttribute("lang", "en-GB");
  // `locale` alone gives navigator.language and the page's own dates, but a native date or time box follows the
  // browser's language: `--lang` in playwright.config.ts. Its text can't be read, but its width shows it. A 24-hour
  // time box ("18:30") is about two thirds as wide as a date box; one with AM/PM ("06:30 PM") over four fifths
  // (71 and 89px against 108px, tried in #278). The date box then reads 05/03/2027, not 03/05/2027.
  await page.setContent('<input type="time" value="18:30"> <input type="date" value="2027-03-05">');
  const width = (type: string) => page.locator(`input[type=${type}]`).evaluate((box) => box.getBoundingClientRect().width);
  expect((await width("time")) / (await width("date"))).toBeLessThan(0.75);
});
