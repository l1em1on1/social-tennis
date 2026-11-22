import { createHash, randomBytes } from "node:crypto";
import { expect, type Page, type TestInfo } from "@playwright/test";
import { Client } from "pg";

/**
 * The email a test logs in with. The local part picks the roles: "manager"
 * and "admin"; anything else has none. The mocked API reads them from the
 * email; against the real API `requestSignInLink` writes them to the database
 * (granting roles in the app is #18).
 * Every login is a new User with no Player: see `addFirstPlayer`.
 *
 * real: a random `+tag` keeps tests from sharing a User in the one database.
 * mock: no tag on a first attempt. Chromatic snapshots the mock project, so
 *   every page that shows the email must render identically. A retry gets a
 *   tag: the mock remembers invites by email for the whole suite, and the
 *   failed attempt may have left one.
 */
export function personaEmail(testInfo: TestInfo, persona: string): string {
  if (testInfo.project.name === "mock") {
    return testInfo.retry === 0 ? `${persona}@example.com` : `${persona}+retry${testInfo.retry}@example.com`;
  }
  return `${persona}+${randomBytes(4).toString("hex")}@example.com`;
}

/**
 * Requests a sign-in link through the real login form and returns the address
 * the email would carry.
 *
 * mock: the mocked API accepts any token, so the email address stands in.
 * real: the e2e stack sends no email, so the test takes the link's place. The
 *   API stores only SHA-256(token); the test overwrites the hash of the token
 *   the form just created with the hash of one it knows. Everything but
 *   delivery runs for real: the form, the API request, the redemption, the
 *   session cookie, /auth/me.
 */
export async function requestSignInLink(page: Page, testInfo: TestInfo, email: string): Promise<string> {
  await page.goto("/login");
  await page.getByLabel("Email address").fill(email);
  await page.getByRole("button", { name: "Email me a sign-in link" }).click();
  await expect(page.getByRole("heading", { name: "Check your email" })).toBeVisible();

  if (testInfo.project.name === "mock") {
    return `/auth/verify?token=${encodeURIComponent(email)}`;
  }
  const token = await takeNewestMagicLink(email);
  await grantRolesNamedInEmail(email);
  return `/auth/verify?token=${encodeURIComponent(token)}`;
}

/** Logs in: requests a link, opens it, and presses "Sign in" on the page it lands on. */
export async function logIn(page: Page, testInfo: TestInfo, email: string) {
  await page.goto(await requestSignInLink(page, testInfo, email));
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Home", level: 1 })).toBeVisible();
}

/**
 * A Player name no other test or retry shares. The Manage page lists every
 * request in the Club, and the stores outlive a test (the real database across
 * runs, the mock's memory across the whole suite), so a fixed name could match
 * a leftover from a failed attempt.
 *
 * mock: the base name on a first attempt, so Chromatic snapshots stay stable.
 * real: always a random suffix.
 */
export function uniqueName(testInfo: TestInfo, base: string): string {
  if (testInfo.project.name === "mock") {
    return testInfo.retry === 0 ? base : `${base} Retry${testInfo.retry}`;
  }
  return `${base} ${randomBytes(3).toString("hex")}`;
}

/** Answers Home's first-sign-in question, which makes `name` the Acting Player. */
export async function addFirstPlayer(page: Page, name: string) {
  await page.getByLabel("Your name").fill(name);
  await page.getByRole("button", { name: "Save my name" }).click();
  await expect(page.getByTestId("acting-player")).toHaveText(name);
}

/** On Profiles: adds another Player to the login. */
export async function addAnotherPlayer(page: Page, name: string) {
  await page.getByRole("button", { name: "Add another player" }).click();
  const sheet = page.getByRole("dialog", { name: "Add a player" });
  await sheet.getByLabel("Name").fill(name);
  await sheet.getByRole("button", { name: "Add player" }).click();
  // .first(): a Player already in a Club lists it in a nested item that names them too.
  await expect(page.getByRole("listitem").filter({ hasText: name }).first()).toBeVisible();
}

/**
 * Stands in for reading the email: makes the newest unused sign-in link for
 * this address redeemable with a token the caller knows, and returns it.
 */
export async function takeNewestMagicLink(email: string): Promise<string> {
  const token = randomBytes(32).toString("base64url");
  await replaceNewestMagicLinkToken(email, token);
  return token;
}

/**
 * UserRoles in the API: Manager = 1, Admin = 2. Runs before the link is
 * redeemed, when the address has no User yet (#108), so it creates the User
 * the redemption will then find.
 */
async function grantRolesNamedInEmail(email: string) {
  const local = email.split("@")[0].split("+")[0].toLowerCase();
  const roles = (local.includes("manager") ? 1 : 0) | (local.includes("admin") ? 2 : 0);
  if (roles === 0) return;

  const client = new Client({ connectionString: process.env.DATABASE_URL });
  await client.connect();
  try {
    await client.query(
      `INSERT INTO "Users" ("Id", "Email", "CreatedAt", "Roles") VALUES (gen_random_uuid(), $2, now(), $1)
       ON CONFLICT ("Email") DO UPDATE SET "Roles" = EXCLUDED."Roles"`,
      [roles, email.toLowerCase()],
    );
  } finally {
    await client.end();
  }
}

async function replaceNewestMagicLinkToken(email: string, rawToken: string) {
  const client = new Client({ connectionString: process.env.DATABASE_URL });
  await client.connect();
  try {
    const hash = createHash("sha256").update(rawToken, "utf8").digest("hex");
    const { rowCount } = await client.query(
      `UPDATE "MagicLinkTokens" SET "TokenHash" = $1
       WHERE "Id" = (
         SELECT t."Id" FROM "MagicLinkTokens" t
         WHERE t."Email" = $2 AND t."UsedAt" IS NULL
         ORDER BY t."CreatedAt" DESC LIMIT 1)`,
      [hash, email.toLowerCase()],
    );
    expect(rowCount, `no unused magic-link token for ${email}`).toBe(1);
  } finally {
    await client.end();
  }
}
