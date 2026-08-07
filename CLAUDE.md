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

## Agent skills

### Issue tracker

Issues live as GitHub Issues in this repo (l1em1on1/social-tennis), using the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage labels

Default five canonical roles, label string equal to role name (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`). See `docs/agents/triage-labels.md`.

### Domain docs

Single-context layout — `CONTEXT.md` + `docs/adr/` at the repo root. See `docs/agents/domain.md`.
