// Chromatic's test captures a DOM snapshot at the end of every test (mock project only;
// see playwright.config.ts), through e2e/support/chromatic.ts, which keeps the bottom
// nav off a long page's content (#182). Otherwise identical to @playwright/test.
import { expect, test } from "./support/chromatic";
import { logIn, personaEmail, requestSignInLink } from "./support/auth";
import { expectNoSeriousA11yViolations } from "./support/axe";

test("an anonymous visitor is sent to the login page", async ({ page }) => {
  await page.goto("/");
  await expect(page).toHaveURL(/\/login$/);
  await expect(page).toHaveTitle("Log in · Hits & Giggles");
  await expectNoSeriousA11yViolations(page);
});

test("opening the emailed link asks for a button press before signing in", async ({ page }, testInfo) => {
  await page.goto(await requestSignInLink(page, testInfo, personaEmail(testInfo, "player")));
  await expect(page).toHaveTitle("Sign in · Hits & Giggles");
  await expect(page.getByRole("button", { name: "Sign in", exact: true })).toBeVisible();
  await expectNoSeriousA11yViolations(page);

  // Opening the link signed nobody in.
  await page.goto("/");
  await expect(page).toHaveURL(/\/login$/);
});

test("a form shows it is working and ignores a second press", async ({ page }, testInfo) => {
  await page.goto("/login");
  await page.getByLabel("Email address").fill(personaEmail(testInfo, "player"));

  // Hold the form's request, as a cold server would.
  let release!: () => void;
  const held = new Promise<void>((resolve) => (release = resolve));
  let posts = 0;
  await page.route("**/login", async (route) => {
    if (route.request().method() === "POST") {
      posts += 1;
      await held;
    }
    await route.continue();
  });

  const button = page.getByRole("button", { name: /Email me a sign-in link|Sending/ });
  await button.click();
  await expect(button).toBeDisabled();
  await expect(button).toHaveText("Sending…");
  await button.click({ force: true });

  release();
  await expect(page.getByRole("heading", { name: "Check your email" })).toBeVisible();
  expect(posts).toBe(1);
});

test("a sign-in address with no token goes back to the login page", async ({ page }) => {
  await page.goto("/auth/verify");
  await expect(page).toHaveURL(/\/login\?error=invalid-link$/);
  await expect(page.getByText("That sign-in link has expired")).toBeVisible();
});

test("a link a mail scanner has already fetched still signs the User in", async ({ page }, testInfo) => {
  test.skip(testInfo.project.name === "mock", "the mocked API redeems any token any number of times");
  const link = await requestSignInLink(page, testInfo, personaEmail(testInfo, "player"));

  // A scanner follows the link with a GET, and doesn't press buttons.
  expect((await page.request.get(link)).ok()).toBe(true);

  await page.goto(link);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Home", level: 1 })).toBeVisible();
});

test("logging in lands on Home, and logging out ends the session", async ({ page }, testInfo) => {
  await logIn(page, testInfo, personaEmail(testInfo, "player"));
  await expect(page).toHaveTitle("Hits & Giggles");
  await expect(page.getByText(/^Signed in as /)).toBeVisible();
  await expectNoSeriousA11yViolations(page);

  await page.getByRole("link", { name: "Profiles" }).click();
  const loggingOut = page.waitForResponse(
    async (response) => /^(__Host-)?st_session=;/m.test((await response.headerValue("set-cookie")) ?? ""),
  );
  await page.getByRole("button", { name: "Log out" }).click();
  await expect(page).toHaveURL(/\/login$/);

  // The browser drops the cookie only if its expiry repeats the attributes it
  // was set with; over https, a `__Host-` expiry without Secure is ignored.
  // This runs over http, so it checks the rest of them: HttpOnly and Path=/
  // come only from the shared cookieOptions(), which also sets Secure.
  const expiry = ((await (await loggingOut).headerValue("set-cookie")) ?? "")
    .split("\n")
    .find((line) => /^(__Host-)?st_session=;/.test(line));
  expect(expiry).toMatch(/Path=\//i);
  expect(expiry).toMatch(/HttpOnly/i);
  expect(expiry).toMatch(/SameSite=lax/i);
  const names = (await page.context().cookies()).map((cookie) => cookie.name);
  expect(names).not.toContain("st_session");
  expect(names).not.toContain("__Host-st_session");

  await page.goto("/");
  await expect(page).toHaveURL(/\/login$/);
});
