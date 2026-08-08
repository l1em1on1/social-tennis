# Web app can mock API content with MSW (JavaScript mocking service)

The Next.js app must be able to run and be tested against **mocked API content**, without the real .NET API being up. We adopt **MSW (Mock Service Worker)** as the mocking layer: it intercepts at the network boundary, so application code — the `openapi-fetch` client, server components, route handlers — runs unchanged and never knows it's talking to a mock.

Two interception modes matter for this stack (ADR-0001): the BFF fetches **server-side**, so day-to-day mocking uses MSW's Node interception (`msw/node`) inside the Next.js server process; the browser service-worker mode is available for pure-client work later. Handlers should be typed against the same generated OpenAPI schema the real client uses (e.g. via `openapi-msw`), so mocks and contract cannot silently drift apart — the codegen pipeline stays the single source of truth.

Mocking is a development and test aid only: enabled by an explicit dev/test flag, excluded from production builds, and never a substitute for the API integration test seam (which stays HTTP-against-real-Postgres, per issue #1's Testing Decisions). This also lays the groundwork for the frontend testing seam that issue #1 deliberately left undefined.

## Considered Options

- **Always develop against the real API via compose** — the current state. Honest, but couples UI work to API availability and makes edge/error states (empty lists, failures, slow responses) laborious to reproduce.
- **Hand-rolled stubs behind the `client.ts` seam** — no dependency, but invents a second fake-API mechanism, bypasses the real fetch path, and drifts from the contract with no type check.
- **MSW** — chosen: network-level interception keeps the real code path, one handler set serves dev, component tests, and future E2E, and schema-typed handlers stay honest to the OpenAPI contract.

## Consequences

`web/` gains MSW as a dev dependency plus a handlers module colocated with the generated schema; a dev/test flag switches interception on (Node mode for the BFF's server-side fetches). Handlers are typed from `schema.d.ts`, so `npm run api:generate` breaking a handler is a feature — the mock is out of date. Production builds must not register MSW. Wiring this up is implementation work for when UI tickets start, not part of the walking skeleton.
