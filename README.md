# Social Tennis

Mobile-first web app for organizing league and social tennis games at a club — Divisional and Knockout leagues, availability-based match scheduling, score agreement, and social sign-ups.

- Domain glossary: [CONTEXT.md](CONTEXT.md) · Decisions: [docs/adr/](docs/adr/) · Spec and tickets: [GitHub issues](https://github.com/l1em1on1/social-tennis/issues)
- Stack: Next.js BFF (`web/`) → ASP.NET Core Web API (`api/`) → Postgres (ADR-0001)

## Prerequisites

**Docker. Nothing else.** No .NET SDK, Node, or npm on the host — every command below runs in a container (ADR-0005). For an IDE experience, open the repo in VS Code and *Reopen in Container*: the [Dev Container](.devcontainer/devcontainer.json) carries both toolchains, the C#/TypeScript language servers, and the Claude Code CLI, and runs its own Docker daemon so the commands below work unchanged from a container terminal.

## Run the stack

```sh
docker compose up --build
```

Postgres, the API, and the web app start together; migrations apply automatically. Open http://localhost:3000.

## Develop (hot reload)

```sh
docker compose -f docker-compose.yml -f docker-compose.dev.yml up
```

Source is bind-mounted: the API restarts on change (`dotnet watch`), the web app hot-reloads (`next dev`).

### Web only, against a mocked API

```sh
MOCK_API=1 docker compose -f docker-compose.yml -f docker-compose.dev.yml up --no-deps web
```

The web app runs with MSW answering every API call in-process (ADR-0009). No api or Postgres is needed. On the login page, "Continue (mock API)" stands in for the email. The part of the email before `@` picks the persona: `manager@…` and `admin@…` get those roles, and `parent@…` holds two child Players. Every primitive is shown at http://localhost:3000/dev/ui.

## Tests

Integration tests drive the API over HTTP against the real compose Postgres (the project's one testing seam — no mocked repositories):

```sh
docker compose run --rm api-tests
```

Frontend end-to-end tests run Playwright in a container against two production builds of the web app, one with the API mocked and one with the real API and Postgres. Accessibility checks (axe) run as part of them:

```sh
docker compose --profile e2e run --rm e2e
```

The HTML report lands in `web/playwright-report/`. Pass Playwright arguments after the service name, e.g. `docker compose --profile e2e run --rm e2e npx playwright test --project=mock`. After changing web code, rebuild the images first with `docker compose --profile e2e build`.

The mocked run also leaves DOM snapshots in `web/test-results/chromatic-archives` for visual review in Chromatic (ADR-0015). CI publishes them when the `CHROMATIC_PROJECT_TOKEN` secret is set. To publish a local run:

```sh
CHROMATIC_PROJECT_TOKEN=<token> docker compose --profile e2e run --rm --no-deps chromatic
```

## Lint and format

CI runs both. ESLint for the web app (`next build` doesn't lint):

```sh
docker compose -f docker-compose.yml -f docker-compose.dev.yml run --rm --no-deps web npm run lint
```

C# formatting and style rules (`api/.editorconfig`). Drop `--verify-no-changes` to apply the fixes:

```sh
docker compose run --rm --no-deps api-tests dotnet format --verify-no-changes
```

The API builds with warnings as errors (`api/Directory.Build.props`), so an analyzer warning fails `api-tests` too.

## Regenerate the TypeScript API client

The typed client (`web/src/lib/api/schema.d.ts`) is generated from the API's OpenAPI document and **committed**. After changing the API surface:

```sh
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d api
docker compose -f docker-compose.yml -f docker-compose.dev.yml run --rm web npm run api:generate
```

Drift check (CI runs this): regenerate, then `git diff --exit-code web/src/lib/api/schema.d.ts`.

## Add a database migration

```sh
docker compose -f docker-compose.yml -f docker-compose.dev.yml run --rm api \
  sh -c "dotnet tool restore && dotnet dotnet-ef migrations add <Name> --project src/SocialTennis.Api"
```

Migrations apply on API startup — there is no separate `database update` step.

## Reset the database

```sh
docker compose down -v   # removes the pgdata volume; next start recreates and re-migrates
```

## License

GPLv3
