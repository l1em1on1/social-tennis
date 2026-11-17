// No page: @playwright/test rather than Chromatic's test, so no snapshot is captured.
import { expect, test } from "@playwright/test";

test("/health answers without a session, and says web can reach the API (#111)", async ({ request }) => {
  const response = await request.get("/health", { maxRedirects: 0 });

  expect(response.status()).toBe(200);
  expect(await response.json()).toEqual({ status: "ok" });
  expect(response.headers()["cache-control"]).toBe("no-store");
});
