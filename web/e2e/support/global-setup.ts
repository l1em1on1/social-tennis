/**
 * Compose's `depends_on` only waits for containers to start, not to serve.
 * Wait here until both web builds answer and the api has finished its
 * startup migration, so the first test doesn't race the stack.
 */
const targets = [
  process.env.E2E_MOCK_URL ?? "http://web-mock:3000",
  process.env.E2E_REAL_URL ?? "http://web-e2e:3000",
].map((origin) => `${origin}/login`);
targets.push(`${process.env.E2E_API_URL ?? "http://api:8080"}/clubs`);

async function waitFor(url: string, timeoutMs = 180_000) {
  const deadline = Date.now() + timeoutMs;
  let last = "no response";
  while (Date.now() < deadline) {
    try {
      const response = await fetch(url);
      if (response.ok) return;
      last = `HTTP ${response.status}`;
    } catch (error) {
      last = error instanceof Error ? error.message : String(error);
    }
    await new Promise((resolve) => setTimeout(resolve, 1000));
  }
  throw new Error(`${url} not ready after ${timeoutMs / 1000}s (${last})`);
}

export default async function globalSetup() {
  await Promise.all(targets.map((url) => waitFor(url)));
}
