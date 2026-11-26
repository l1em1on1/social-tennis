---
status: accepted
---

# One app for every environment: stand-ins are separate services, reached by address

The web app had code for an environment it wasn't in. A build argument (`MOCK_API`) put MSW inside the app, and the login page, `/health`, `/dev/ui` and the server start-up branched on it. A normal build compiled all of that out, and CI checked the production image, so production was never exposed. But one build argument decided which app was built, and the mocked e2e run and Chromatic tested a different build from the one shipped. The owner (2026-11-24, #233): production and every other environment should run the same code.

## Decision

- **The web app is one image, built one way.** `web`, `web-e2e`, `web-mock` and production use the same Dockerfile, with no build arguments. What differs between environments is settings, read at run time.
- **The app doesn't ask which environment it is in.** An ESLint rule fails on `NODE_ENV` or `MOCK_API` in `src/` and `next.config.ts`, and on importing MSW or the mock. The one exception is `src/proxy.ts`: under `next dev` the Content-Security-Policy allows `eval`, which React's tools there need. A build fixes that to off, where a run-time setting could weaken the policy in production.
- **A stand-in is a separate service, reached by its address.** The mock API is `web/mock-api/`: the MSW handlers of ADR-0009, still typed by the OpenAPI schema, behind a plain HTTP server. The app reaches it through `API_URL`, as it reaches the real API.
- **It isn't in the web image's build context**, publishes no port and is never deployed. CI checks the production image for it.
- **It fails loudly.** A request with no handler is logged and its connection dropped, so the page fails as it would if the API were down.
- **Signing in against it** works as with the real API locally: the link is in the service's log.

## Considered Options

- **Keep MSW in the app, behind the flag** (ADR-0009 until now). Compiled out of production, but the app carries the conditions and the tests run another build.
- **`@msw/serve`**, MSW's own standalone server. One 0.x release so far, and its docs don't say what an unhandled request gets. MSW's `getResponse` with Node's `http` is about fifty lines.
- **Only the real API, no mock.** Chromatic needs the same data and dates every run. From the real API that takes a test clock inside it: the kind of condition this removes. Web work would also always need the API running. Whether the mock should shrink is #234.
- **A fake email service too**, so that the API's `Log` email provider could go. The owner chose to leave email as it is: the provider is configuration, and outside Development the API refuses to start with it unless it is allowed explicitly (#107).

## Consequences

- The e2e stack has one more service, `mock-api`. Its state lasts as long as its container, so a second run starts by recreating `mock-api`; `web-mock` can stay up.
- A change to the mock rebuilds a small image, not the web app.
- web's `/health` runs its real code in the mocked stack.
- `fixture.ts`, the fixed world of ADR-0015's snapshots, is now `web/mock-api/fixture.ts`.
- The API keeps ASP.NET's own Development mode, used only under `dotnet watch`: readable logs and the framework's error page. Compose and production both run it as Production.

**Amended (2026-11-26): the mock API keeps fixed data only (#234).** It was about 2,700 lines that repeated the API's rules by hand, so every rule changed twice. The owner chose fixed data (2026-11-25; plan agreed 2026-11-26).

- **What it does now.** It answers every read from one fixed world (`mock-api/fixture.ts`), as the real API would for the User signed in, and signs in and out. Every other write answers 501 `fixed-data`, so a test that tries one fails loudly. It repeats no API rule.
- **Who uses it.** The e2e `mock` project, which only looks: one test per screen and state (`screens.spec.ts`, `manager-pages.spec.ts`), for Chromatic. Every flow runs in the `real` project only. Running web alone against it is for looking at screens; clicking through flows takes the real API.
- **Considered: keep it** (every spec in both projects), and **remove it** (snapshots from the real API, which needs a test clock in the API). The first kept the double work; the second adds test-only code to the API.
- **Consequence.** A new screen or state for review goes into the fixture and gets a test in `screens.spec.ts`. The mock no longer needs recreating between runs: it holds no state.
