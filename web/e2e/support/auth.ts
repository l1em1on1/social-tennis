import { createHash, randomBytes } from "node:crypto";
import { expect, type Page, type TestInfo } from "@playwright/test";
import { Client } from "pg";

/**
 * The email a test logs in with. The local part picks the stub roles
 * (lib/viewer/stub.ts): "manager" and "admin"; anything else has none.
 * Every login is a new User with no Player: see `addFirstPlayer`.
 *
 * real: a random `+tag` keeps tests from sharing a User in the one database.
 * mock: no tag. Nothing is shared there, and Chromatic snapshots the mock
 *   project, so every page that shows the email must render identically.
 */
export function personaEmail(testInfo: TestInfo, persona: string): string {
  return testInfo.project.name === "mock"
    ? `${persona}@example.com`
    : `${persona}+${randomBytes(4).toString("hex")}@example.com`;
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
  const token = randomBytes(32).toString("base64url");
  await replaceNewestMagicLinkToken(email, token);
  return `/auth/verify?token=${encodeURIComponent(token)}`;
}

/** Logs in: requests a link, opens it, and presses "Sign in" on the page it lands on. */
export async function logIn(page: Page, testInfo: TestInfo, email: string) {
  await page.goto(await requestSignInLink(page, testInfo, email));
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Home", level: 1 })).toBeVisible();
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
  await expect(page.getByRole("listitem").filter({ hasText: name })).toBeVisible();
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
         JOIN "Users" u ON u."Id" = t."UserId"
         WHERE u."Email" = $2 AND t."UsedAt" IS NULL
         ORDER BY t."CreatedAt" DESC LIMIT 1)`,
      [hash, email.toLowerCase()],
    );
    expect(rowCount, `no unused magic-link token for ${email}`).toBe(1);
  } finally {
    await client.end();
  }
}
