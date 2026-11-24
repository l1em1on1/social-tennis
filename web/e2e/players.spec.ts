// Chromatic's test captures a DOM snapshot at the end of every test (mock project only;
// see playwright.config.ts), through e2e/support/chromatic.ts, which keeps the bottom
// nav off a long page's content (#182). Otherwise identical to @playwright/test.
import { expect, test } from "./support/chromatic";
import { addFirstPlayer, emailedLinkFor, logIn, personaEmail, uniqueName } from "./support/auth";
import { sharedJoinLink } from "./support/join";
import { expectNoSeriousA11yViolations } from "./support/axe";

test("a first sign-in asks for a name; name, gender and level are edited in Profiles", async ({ page }, testInfo) => {
  await logIn(page, testInfo, personaEmail(testInfo, "player"));

  // Nobody to play as yet: no "Playing as", and the question is on Home.
  await expect(page.getByTestId("acting-player")).toHaveCount(0);
  await expect(page.getByRole("heading", { name: "Welcome! What's your name?" })).toBeVisible();
  await expectNoSeriousA11yViolations(page);

  await addFirstPlayer(page, "Sam Smith");
  await expect(page.getByRole("heading", { name: "Welcome! What's your name?" })).toHaveCount(0);

  await page.getByRole("navigation", { name: "Main" }).getByRole("link", { name: "Profiles" }).click();
  await page.getByRole("button", { name: "Edit Sam Smith" }).click();
  const sheet = page.getByRole("dialog", { name: "Edit Sam Smith" });
  await expectNoSeriousA11yViolations(page);
  await sheet.getByLabel("Name").fill("Samantha Smith");
  await sheet.getByLabel("Plays as").selectOption("Female");
  await sheet.getByLabel("Level").selectOption("3");
  await sheet.getByRole("button", { name: "Save" }).click();

  await expect(page.getByTestId("acting-player")).toHaveText("Samantha Smith");
  await expect(page.getByRole("dialog")).toHaveCount(0);

  await expect(page.getByText("Female · Level 3")).toBeVisible();

  // It is stored, not just shown.
  await page.reload();
  await expect(page.getByTestId("acting-player")).toHaveText("Samantha Smith");
  await expect(page.getByText("Female · Level 3")).toBeVisible();
});

test("a name of only spaces is refused with a message", async ({ page }, testInfo) => {
  await logIn(page, testInfo, personaEmail(testInfo, "player"));
  await page.getByLabel("Your name").fill("   ");
  await page.getByRole("button", { name: "Save my name" }).click();

  await expect(page.getByText("That name didn't work")).toBeVisible();
  await expect(page.getByTestId("acting-player")).toHaveCount(0);
});

test("a Player asks to join through the Club's link, and is a member once a Manager approves", async ({
  page,
  browser,
}, testInfo) => {
  const robin = uniqueName(testInfo, "Robin Requester");
  // A Manager, in a browser of their own, shares the Club's join link (#222).
  const managerContext = await browser.newContext();
  const manager = await managerContext.newPage();
  await logIn(manager, testInfo, personaEmail(testInfo, "manager"));
  const { path, clubName } = await sharedJoinLink(manager);
  await expectNoSeriousA11yViolations(manager);

  // Home lists no Clubs: it says how to get in.
  await logIn(page, testInfo, personaEmail(testInfo, "player"));
  await addFirstPlayer(page, robin);
  await expect(page.getByRole("heading", { name: "Join your club" })).toBeVisible();
  await expect(page.getByRole("button", { name: /^Ask to join / })).toHaveCount(0);

  // Opening the link asks nothing yet; the button does.
  await page.goto(path);
  await expect(page.getByRole("heading", { name: `Join ${clubName}`, level: 1 })).toBeVisible();
  await expectNoSeriousA11yViolations(page);
  await page.getByRole("button", { name: `Ask to join ${clubName}` }).click();
  await expect(page.getByRole("heading", { name: "Waiting for approval" })).toBeVisible();
  await page.getByRole("navigation", { name: "Main" }).getByRole("link", { name: "Home" }).click();
  await expect(page.getByRole("heading", { name: "Waiting to join" })).toBeVisible();

  await manager.reload();
  await manager.getByRole("button", { name: `Approve ${robin}` }).click();
  await expect(manager.getByRole("button", { name: `Approve ${robin}` })).toHaveCount(0);
  await managerContext.close();

  await page.reload();
  await expect(page.getByRole("heading", { name: "Waiting to join" })).toHaveCount(0);
  await expect(page.getByText("No games yet")).toBeVisible();
  await page.goto(path);
  await expect(page.getByRole("heading", { name: `${robin} is a member` })).toBeVisible();
  await page.getByRole("navigation", { name: "Main" }).getByRole("link", { name: "Profiles" }).click();
  await expect(page.getByText(`Member of ${clubName}`, { exact: true })).toBeVisible();
  await expectNoSeriousA11yViolations(page);
});

test("someone signed out opens a Club's join link, signs in, and is back on it to ask", async ({ page, browser }, testInfo) => {
  const sky = uniqueName(testInfo, "Sky Linked");
  const email = personaEmail(testInfo, "linked");
  const managerContext = await browser.newContext();
  const manager = await managerContext.newPage();
  await logIn(manager, testInfo, personaEmail(testInfo, "manager"));
  const { path, clubName } = await sharedJoinLink(manager);
  await managerContext.close();
  const code = path.split("/").pop()!;

  // Signed out, the link sends them to sign in, and the sign-in carries the link's code.
  await page.goto(path);
  await expect(page).toHaveURL(`/login?join=${code}`);
  await expect(page.getByText("Sign in first, then you can ask to join the club.")).toBeVisible();
  await page.getByLabel("Email address").fill(email);
  await page.getByRole("button", { name: "Email me a sign-in link" }).click();
  await expect(page.getByRole("heading", { name: "Check your email" })).toBeVisible();

  // The emailed link opens the join page once they're in: a name, then the button.
  await page.goto(await emailedLinkFor(testInfo, email, code));
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(path);
  await expect(page.getByRole("heading", { name: `Join ${clubName}`, level: 1 })).toBeVisible();
  await addFirstPlayer(page, sky);
  await expect(page).toHaveURL(path);
  await page.getByRole("button", { name: `Ask to join ${clubName}` }).click();
  await expect(page.getByRole("heading", { name: "Waiting for approval" })).toBeVisible();
});

test("a session that has ended keeps a join link's code on the way to sign in", async ({ page }, testInfo) => {
  const code = "AAAAAAAAAAAAAAAAAAAAAA";
  // A session cookie the API no longer honours: the proxy lets it through, and the page's 401 sends it to sign in (#223 review).
  await page.context().addCookies([{ name: "st_session", value: "ended", url: testInfo.project.use.baseURL! }]);

  await page.goto(`/join/${code}`);
  await expect(page).toHaveURL(`/login?join=${code}`);
  await expect(page.getByText("Sign in first, then you can ask to join the club.")).toBeVisible();

  // A sign-in link that lost its token keeps the code too.
  await page.goto(`/auth/verify?join=${code}`);
  await expect(page).toHaveURL(`/login?error=invalid-link&join=${code}`);
});

test("a link that's off, reset or unknown doesn't work, and says only that", async ({ page }, testInfo) => {
  await logIn(page, testInfo, personaEmail(testInfo, "player"));

  await page.goto("/join/AAAAAAAAAAAAAAAAAAAAAA");

  await expect(page.getByRole("heading", { name: "This link doesn't work any more" })).toBeVisible();
  await expect(page.getByText("Ask your club's manager for a new one.")).toBeVisible();
  await expect(page.getByRole("button", { name: /^Ask to join / })).toHaveCount(0);
});

test("a Manager declines a request, and the Player can ask again", async ({ page, browser }, testInfo) => {
  const dana = uniqueName(testInfo, "Dana Declined");
  const managerContext = await browser.newContext();
  const manager = await managerContext.newPage();
  await logIn(manager, testInfo, personaEmail(testInfo, "manager"));
  const { path, clubName } = await sharedJoinLink(manager);

  await logIn(page, testInfo, personaEmail(testInfo, "player"));
  await addFirstPlayer(page, dana);
  await page.goto(path);
  await page.getByRole("button", { name: `Ask to join ${clubName}` }).click();
  await expect(page.getByRole("heading", { name: "Waiting for approval" })).toBeVisible();

  await manager.reload();
  await manager.getByRole("button", { name: `Decline ${dana}` }).click();
  await expect(manager.getByText(dana)).toHaveCount(0);
  await managerContext.close();

  await page.reload();
  await expect(page.getByRole("button", { name: `Ask to join ${clubName}` })).toBeVisible();
});

test("someone a Manager invited by email is in the Club as soon as they save their name", async ({
  page,
  browser,
}, testInfo) => {
  const ivy = uniqueName(testInfo, "Ivy Invited");
  const email = personaEmail(testInfo, "invited");

  const managerContext = await browser.newContext();
  const manager = await managerContext.newPage();
  await logIn(manager, testInfo, personaEmail(testInfo, "manager"));
  await manager.goto("/manage");
  // The first Club listed: the Admin specs create others, which sort after it.
  const clubName = (await manager.getByRole("heading", { level: 2 }).first().textContent()) ?? "";
  await manager.getByLabel("Their email address").first().fill(email);
  await manager.getByRole("button", { name: "Add", exact: true }).first().click();
  await expect(manager.getByRole("status")).toContainText(`${email} is pre-approved`);
  const invite = manager.getByRole("list", { name: "Pre-approved addresses" }).getByRole("listitem").filter({ hasText: email });
  await expect(invite).toBeVisible();

  await logIn(page, testInfo, email);
  await addFirstPlayer(page, ivy);
  await expect(page.getByText("No games yet")).toBeVisible();
  await expect(page.getByRole("heading", { name: "Join your club" })).toHaveCount(0);
  await page.getByRole("navigation", { name: "Main" }).getByRole("link", { name: "Profiles" }).click();
  await expect(page.getByText(`Member of ${clubName}`, { exact: true })).toBeVisible();

  // Letting Ivy in used the invite up (#164).
  await manager.reload();
  await expect(invite).toHaveCount(0);
  await managerContext.close();
});

test("a Manager removes a member, and withdraws an invite", async ({ page, browser }, testInfo) => {
  const mona = uniqueName(testInfo, "Mona Member");
  const managerContext = await browser.newContext();
  const manager = await managerContext.newPage();
  await logIn(manager, testInfo, personaEmail(testInfo, "manager"));
  const { path, clubName } = await sharedJoinLink(manager);

  await logIn(page, testInfo, personaEmail(testInfo, "player"));
  await addFirstPlayer(page, mona);
  await page.goto(path);
  await page.getByRole("button", { name: `Ask to join ${clubName}` }).click();
  await expect(page.getByRole("heading", { name: "Waiting for approval" })).toBeVisible();

  await manager.reload();
  await manager.getByRole("button", { name: `Approve ${mona}` }).click();

  // Removing asks first.
  await manager.getByRole("button", { name: `Remove ${mona}` }).click();
  const sheet = manager.getByRole("dialog", { name: `Remove ${mona}?` });
  await expect(sheet).toBeVisible();
  await expectNoSeriousA11yViolations(manager);
  await sheet.getByRole("button", { name: "Remove from club" }).click();
  await expect(manager.getByText(mona)).toHaveCount(0);

  // An invite nobody has used can be taken back.
  const email = personaEmail(testInfo, "withdrawn");
  await manager.getByLabel("Their email address").first().fill(email);
  await manager.getByRole("button", { name: "Add", exact: true }).first().click();
  await expect(manager.getByRole("status")).toContainText(`${email} is pre-approved`);
  await manager.getByRole("button", { name: `Withdraw pre-approval for ${email}` }).click();
  await expect(manager.getByRole("list", { name: "Pre-approved addresses" }).getByText(email)).toHaveCount(0);
  await managerContext.close();

  await page.goto("/");
  await expect(page.getByRole("heading", { name: "Join your club" })).toBeVisible();
});

test("a Player takes back a request, and a member leaves a Club", async ({ page, browser }, testInfo) => {
  const lee = uniqueName(testInfo, "Lee Leaver");
  const nav = page.getByRole("navigation", { name: "Main" });
  const managerContext = await browser.newContext();
  const manager = await managerContext.newPage();
  await logIn(manager, testInfo, personaEmail(testInfo, "manager"));
  const { path, clubName } = await sharedJoinLink(manager);
  await logIn(page, testInfo, personaEmail(testInfo, "player"));
  await addFirstPlayer(page, lee);

  // Ask, then think better of it.
  await page.goto(path);
  await page.getByRole("button", { name: `Ask to join ${clubName}` }).click();
  await expect(page.getByRole("heading", { name: "Waiting for approval" })).toBeVisible();
  await nav.getByRole("link", { name: "Profiles" }).click();
  await page.getByRole("button", { name: /^Cancel request to join / }).click();
  await expect(page.getByText("Not in a club yet")).toBeVisible();

  // Ask again, and this time a Manager approves.
  await page.goto(path);
  await page.getByRole("button", { name: `Ask to join ${clubName}` }).click();
  await expect(page.getByRole("heading", { name: "Waiting for approval" })).toBeVisible();
  await manager.reload();
  await manager.getByRole("button", { name: `Approve ${lee}` }).click();
  await expect(manager.getByRole("button", { name: `Remove ${lee}` })).toBeVisible();
  await managerContext.close();

  await nav.getByRole("link", { name: "Profiles" }).click();
  await page.getByRole("button", { name: /^Leave / }).click();
  const sheet = page.getByRole("dialog", { name: /^Leave .+\?$/ });
  await expect(sheet).toBeVisible();
  await expectNoSeriousA11yViolations(page);
  await sheet.getByRole("button", { name: "Leave club" }).click();
  await expect(page.getByText("Not in a club yet")).toBeVisible();
});

test("a Manager adds a Player by name and email, and they hold it on signing in", async ({
  page,
  browser,
}, testInfo) => {
  const child = uniqueName(testInfo, "Casey Child");
  const email = personaEmail(testInfo, "newparent");

  const managerContext = await browser.newContext();
  const manager = await managerContext.newPage();
  await logIn(manager, testInfo, personaEmail(testInfo, "manager"));
  await manager.goto("/manage");
  // The first Club listed: the Admin specs create others, which sort after it.
  const clubName = (await manager.getByRole("heading", { level: 2 }).first().textContent()) ?? "";
  await manager.getByLabel("Name (optional)").first().fill(child);
  await manager.getByLabel("Their email address").first().fill(email);
  await manager.getByRole("button", { name: "Add", exact: true }).first().click();
  await expect(manager.getByRole("status")).toContainText(`${child} is added`);

  // Listed under the login that will hold it, which nobody has used yet.
  const held = manager.getByRole("list", { name: `Players of ${email}` }).first();
  await expect(held.getByText(child, { exact: true }).first()).toBeVisible();

  // Rating is hidden from the UI for now (#161).
  await expect(manager.getByText(/\brating\b/i)).toHaveCount(0);
  await expectNoSeriousA11yViolations(manager);
  await managerContext.close();

  // Signing in with that address: the Player is theirs, already a member, with no name to ask for.
  await logIn(page, testInfo, email);
  await expect(page.getByTestId("acting-player")).toHaveText(child);
  await expect(page.getByRole("heading", { name: "Welcome! What's your name?" })).toHaveCount(0);
  await expect(page.getByRole("heading", { name: "Join your club" })).toHaveCount(0);

  // Their Rating is the Manager's business: Profiles neither shows nor edits it.
  await page.getByRole("navigation", { name: "Main" }).getByRole("link", { name: "Profiles" }).click();
  await expect(page.getByText(`Member of ${clubName}`, { exact: true })).toBeVisible();
  await expect(page.getByText(child, { exact: true }).first()).toBeVisible();
  await expect(page.getByText(/rating/i)).toHaveCount(0);
  await page.getByRole("button", { name: `Edit ${child}` }).click();
  await expect(page.getByRole("dialog", { name: `Edit ${child}` }).getByLabel(/rating/i)).toHaveCount(0);
});

test("a Player can't open the Manager's members list", async ({ page }, testInfo) => {
  await logIn(page, testInfo, personaEmail(testInfo, "player"));
  await page.goto("/manage");
  await expect(page.getByRole("heading", { name: "Page not found" })).toBeVisible();
});
