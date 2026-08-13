# Project: Tennis League and Social Games

Tennis League and Social Games — an application for organizing and planning league and social tennis games.

Easy to use mobile first web application to manage social leagues, gather match stats and to allow users join leagues, track scores, and find best suitable dates for the game. 

- **License**: GPLv3
- **Repository**: `git@github.com:l1em1on1/social-tennis.git`

## Everything runs in Docker

**Never run `dotnet`, `npm`, `node`, `npx`, or EF tooling directly on the host machine.** The host has Docker and an editor; it does not have the toolchain, and must not be assumed to.

Every build, test, migration, package install, and codegen run executes inside a container — `docker compose run --rm <service> <command>`, or the equivalent from inside the Dev Container. This applies to agents exactly as it does to humans.

When adding a workflow, document it as a container invocation. A bare `dotnet test` or `npm install` in a README, script, or CI step is a bug.

Stack is a Next.js BFF (`web/`) proxying to an ASP.NET Core Web API (`api/`) over Postgres. See `docs/adr/` — ADR-0001 for the split, ADR-0005 for the Docker requirement.

## Library docs via Context7 — required for code work

Before writing, changing, or reviewing code that touches a library or framework (Next.js, React, EF Core, ASP.NET Core, Npgsql, openapi-fetch/openapi-typescript, Tailwind, xUnit, …), query the **Context7 MCP** for that library's current documentation: `resolve-library-id` → `query-docs`, one focused question per concept. Training data goes stale; follow what Context7's docs say over memory, and when the two conflict, the docs win. Cite the doc-backed reason in the code comment or PR description when a Context7 finding changes a decision.

Next.js additionally ships in-package agent docs at `web/node_modules/next/dist/docs/` — read the relevant guide before writing Next.js code; conventions may differ from training data.

## Code intelligence via LSP — symbols, not text

The `csharp-lsp` and `typescript-lsp` plugins are installed. Use the `LSP` tool wherever the question is about a **symbol** rather than a string:

- **Before renaming anything or changing a signature**, `findReferences`. Grep finds matches in comments and strings and misses call sites spelled differently — the contract renames in #29 are exactly the case where a missed reference reaches CI instead of the editor.
- **`goToDefinition` / `hover`** to resolve what a type actually is, rather than tracing `using` and `import` chains by hand.
- **`hover` for a type question on `.cs`**, where it pays most: checking it by compiling costs a `docker compose run`, and hover costs nothing.

This is **not** a loophole in the Docker rule, because the servers are not on the host. **Work from inside the Dev Container** — `.devcontainer/post-create.sh` installs `csharp-ls`, `typescript-language-server`, and the Claude Code CLI itself, so an agent gets symbol intelligence with nothing but Docker on the host. Builds, tests, migrations, and package installs still run as compose services, always.

The container uses **docker-in-docker**, so `docker compose` works unchanged from a container terminal: the inner daemon resolves the relative bind mounts (`./api:/src`) against the container's own filesystem. Running compose from *outside* the container works too, from a host terminal.

An LSP call failing with "command not found" means the session is running on the host rather than in the Dev Container — reopen in the container. Do not install the servers to fix it: `dotnet tool install` and `npm install -g` on the host are exactly what the Docker rule forbids. Say which it is and fall back to grep.

## Keep `docs/architecture.md` current

`docs/architecture.md` is the technical documentation of the system as built — topology, request flow, contract pipeline, repo layout, runtime configurations, testing seam, version pins. **Any change that alters one of those must update the document (including its mermaid diagrams) in the same commit.** Concretely: adding/removing a service or endpoint surface, changing how the TS client is generated, moving folders, changing compose/devcontainer behaviour, bumping a pinned version, or changing how tests run. A stale architecture doc is a bug, same as a stale README command.

## Agent skills

### Issue tracker

Issues live as GitHub Issues in this repo (l1em1on1/social-tennis), using the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage labels

Default five canonical roles, label string equal to role name (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`). See `docs/agents/triage-labels.md`.

### API endpoints

One static class per endpoint in a feature folder — never lambdas in `Program.cs` or in the feature extension. See `docs/agents/api-endpoints.md` (reasoning: ADR-0010).

### Domain docs

Single-context layout — `CONTEXT.md` + `docs/adr/` at the repo root. See `docs/agents/domain.md`.
