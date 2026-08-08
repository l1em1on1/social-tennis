# Social Tennis

Mobile-first web app for organizing league and social tennis games at a club — Divisional and Knockout leagues, availability-based match scheduling, score agreement, and social sign-ups.

- Domain glossary: [CONTEXT.md](CONTEXT.md) · Decisions: [docs/adr/](docs/adr/) · Spec and tickets: [GitHub issues](https://github.com/l1em1on1/social-tennis/issues)
- Stack: Next.js BFF (`web/`) → ASP.NET Core Web API (`api/`) → Postgres (ADR-0001)

## Prerequisites

**Docker. Nothing else.** No .NET SDK, Node, or npm on the host — every command below runs in a container (ADR-0005). For an IDE experience, open the repo in VS Code and *Reopen in Container*: the [Dev Container](.devcontainer/devcontainer.json) carries both toolchains.

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

## Tests

Integration tests drive the API over HTTP against the real compose Postgres (the project's one testing seam — no mocked repositories):

```sh
docker compose run --rm api-tests
```

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
