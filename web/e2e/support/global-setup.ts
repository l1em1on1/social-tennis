/**
 * Compose already holds the runner back until both web builds and the api
 * report healthy. This repeats the wait for runs that bypass that: a target
 * overridden with E2E_*_URL, or `run --no-deps`.
 */
const targets = [
  process.env.E2E_MOCK_URL ?? "http://web-mock:3000",
  process.env.E2E_REAL_URL ?? "http://web-e2e:3000",
].map((origin) => `${origin}/login`);
targets.push(`${process.env.E2E_API_URL ?? "http://api:8080"}/health`);

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
