import { createHash, randomBytes } from "node:crypto";
import { expect, type Page, type TestInfo } from "@playwright/test";
import { Client } from "pg";

/**
 * The email a test logs in with. The local part picks the stub persona
 * (lib/viewer/stub.ts): "player", "manager", "admin", "parent".
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
 * Logs in through the real login form, then follows the emailed link.
 *
 * mock: the mocked API accepts any token; the page offers a "Continue" link.
 * real: there's no inbox yet (#19), so the test takes the link's place. The
 *   API stores only SHA-256(token); the test overwrites the hash of the token
 *   the form just created with the hash of one it knows, then redeems that.
 *   Everything but delivery runs for real: the form, the API request, the
 *   redemption, the session cookie, /auth/me.
 */
export async function logIn(page: Page, testInfo: TestInfo, email: string) {
  await page.goto("/login");
  await page.getByLabel("Email address").fill(email);
  await page.getByRole("button", { name: "Email me a sign-in link" }).click();
  await expect(page.getByRole("heading", { name: "Check your email" })).toBeVisible();

  if (testInfo.project.name === "mock") {
    await page.getByRole("link", { name: "Continue (mock API)" }).click();
  } else {
    const token = randomBytes(32).toString("base64url");
    await replaceNewestMagicLinkToken(email, token);
    await page.goto(`/auth/verify?token=${encodeURIComponent(token)}`);
  }

  await expect(page.getByRole("heading", { name: "Home", level: 1 })).toBeVisible();
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
