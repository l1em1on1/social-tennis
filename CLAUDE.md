# Hits & Giggles: tennis leagues and socials

A mobile-first web app where club members join leagues, arrange games and record scores. A Next.js BFF (`web/`) proxies to an ASP.NET Core API (`api/`) over Postgres (ADR-0001). GPLv3.

## Docker for every build

Run every build, test, migration, package install and codegen as a compose service: `docker compose run --rm <service> <command>` (ADR-0005). The host has Docker and an editor, no toolchain. Write every new workflow, script, README command and CI step the same way.

## Library docs first

Before writing, changing or reviewing code that uses a library or framework (Next.js, React, EF Core, ASP.NET Core, Npgsql, openapi-fetch, openapi-typescript, Tailwind, xUnit, …), query Context7: `resolve-library-id`, then `query-docs`, one question per concept. The docs outrank memory; when they change a decision, cite them in the code comment or PR. For Next.js, also read the guide in `web/node_modules/next/dist/docs/`.

## Symbols through LSP

Use the `LSP` tool for questions about a symbol:

- `findReferences` before renaming anything or changing a signature.
- `goToDefinition` and `hover` to see what a type is; on `.cs`, instead of compiling.

The language servers run in the Docker Sandbox (`./sandbox.sh`, `.sbx/README.md`); builds still run in compose. If C# lookups report every package type missing, run `/reload-plugins`. An LSP "command not found" means the session is on the host: say so, and use grep.

## `docs/architecture.md` moves with the code

It documents the system as built: topology, request flow, contract pipeline, repo layout, runtime configuration, testing seam, version pins, and its mermaid diagrams. A change to any of these (a service or endpoint surface, how the client is generated, folders, compose or sandbox behaviour, a pinned version, how tests run) updates it in the same commit.

## Reports

Report in a few lines: what changed, what was checked (the command and its result), and what the owner needs to do. Leave out your own steps and workarounds. Reasons belong in an ADR: link it.

## Where to look

- Issues, PRs and their bodies: GitHub through `gh`, in `docs/agents/issue-tracker.md`.
- Triage labels: `docs/agents/triage-labels.md`.
- API endpoints, one static class each (ADR-0010): `docs/agents/api-endpoints.md`.
- Domain language, `CONTEXT.md` and `docs/adr/`: `docs/agents/domain.md`.

## RTK

RTK compacts command output; its hook rewrites Bash commands (`git status` becomes `rtk git status`). Wrap a compose run to see only failures, and use `rtk proxy <cmd>` for the raw output:

```bash
rtk test docker compose --profile e2e run --rm e2e       # Playwright
rtk test docker compose run --rm api-tests                # xUnit
rtk err docker compose build web                          # build errors
```
