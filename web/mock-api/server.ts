import { createServer, type IncomingMessage, type ServerResponse } from "node:http";
import { getResponse } from "msw";
import { handlers } from "./handlers";

/**
 * The mock API: the MSW handlers behind a plain HTTP server (ADR-0009,
 * ADR-0033). The web app reaches it through API_URL, as it reaches the real
 * API, and nothing in the app knows which one it has.
 */
const PORT = 8080;

/** What the handlers' relative paths resolve against (MSW docs, `getResponse`). Never contacted. */
const ORIGIN = "http://mock-api";

async function toRequest(incoming: IncomingMessage): Promise<Request> {
  const headers = new Headers();
  for (const [name, value] of Object.entries(incoming.headers)) {
    for (const each of [value ?? []].flat()) headers.append(name, each);
  }
  const chunks: Buffer[] = [];
  for await (const chunk of incoming) chunks.push(chunk as Buffer);
  return new Request(new URL(incoming.url ?? "/", ORIGIN), {
    method: incoming.method,
    headers,
    body: chunks.length > 0 ? Buffer.concat(chunks) : undefined,
  });
}

/**
 * A request with no handler is a gap in the mock, and a handler that throws is
 * a bug in it. Neither may look like an answer from the API, so the connection
 * is dropped: the page's fetch fails, and the log says why.
 */
function refuse(incoming: IncomingMessage, outgoing: ServerResponse, reason: unknown) {
  const line = `${incoming.method} ${incoming.url}`.replace(/[\r\n]/g, "");
  console.error("[mock-api] %s:", line, reason);
  outgoing.destroy();
}

const server = createServer(async (incoming, outgoing) => {
  try {
    const response = await getResponse(handlers, await toRequest(incoming), { baseUrl: ORIGIN });
    if (!response) {
      refuse(incoming, outgoing, "no handler");
      return;
    }
    outgoing.writeHead(response.status, Object.fromEntries(response.headers));
    outgoing.end(Buffer.from(await response.arrayBuffer()));
  } catch (error) {
    refuse(incoming, outgoing, error);
  }
});

// Longer than any gap between two of the suite's calls, so the web app doesn't
// send one on a connection this end is just closing.
server.keepAliveTimeout = 120_000;

// As PID 1 in its container, Node ignores SIGTERM unless told otherwise, and
// `docker compose down` would wait ten seconds to kill it.
process.on("SIGTERM", () => process.exit(0));

server.listen(PORT, () => console.info(`[mock-api] listening on ${PORT}`));
