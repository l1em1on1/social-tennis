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

<!-- rtk-instructions v2 -->
# RTK (Rust Token Killer) - Token-Optimized Commands

## Golden Rule

**Always prefix commands with `rtk`**. If RTK has a dedicated filter, it uses it. If not, it passes through unchanged. This means RTK is always safe to use.

**Important**: Even in command chains with `&&`, use `rtk`:
```bash
# ❌ Wrong
git add . && git commit -m "msg" && git push

# ✅ Correct
rtk git add . && rtk git commit -m "msg" && rtk git push
```

## RTK Commands by Workflow

### Build & Compile (80-90% savings)
```bash
rtk cargo build         # Cargo build output
rtk cargo check         # Cargo check output
rtk cargo clippy        # Clippy warnings grouped by file (80%)
rtk tsc                 # TypeScript errors grouped by file/code (83%)
rtk lint                # ESLint/Biome violations grouped (84%)
rtk prettier --check    # Files needing format only (70%)
rtk next build          # Next.js build with route metrics (87%)
```

### Test (60-99% savings)
```bash
rtk cargo test          # Cargo test failures only (90%)
rtk go test             # Go test failures only (90%)
rtk jest                # Jest failures only (99.5%)
rtk vitest              # Vitest failures only (99.5%)
rtk playwright test     # Playwright failures only (94%)
rtk pytest              # Python test failures only (90%)
rtk rake test           # Ruby test failures only (90%)
rtk rspec               # RSpec test failures only (60%)
rtk test <cmd>          # Generic test wrapper - failures only
```

### Git (59-80% savings)
```bash
rtk git status          # Compact status
rtk git log             # Compact log (works with all git flags)
rtk git diff            # Compact diff (80%)
rtk git show            # Compact show (80%)
rtk git add             # Ultra-compact confirmations (59%)
rtk git commit          # Ultra-compact confirmations (59%)
rtk git push            # Ultra-compact confirmations
rtk git pull            # Ultra-compact confirmations
rtk git branch          # Compact branch list
rtk git fetch           # Compact fetch
rtk git stash           # Compact stash
rtk git worktree        # Compact worktree
```

Note: Git passthrough works for ALL subcommands, even those not explicitly listed.

### GitHub (26-87% savings)
```bash
rtk gh pr view <num>    # Compact PR view (87%)
rtk gh pr checks        # Compact PR checks (79%)
rtk gh run list         # Compact workflow runs (82%)
rtk gh issue list       # Compact issue list (80%)
rtk gh api              # Compact API responses (26%)
```

### JavaScript/TypeScript Tooling (70-90% savings)
```bash
rtk pnpm list           # Compact dependency tree (70%)
rtk pnpm outdated       # Compact outdated packages (80%)
rtk pnpm install        # Compact install output (90%)
rtk npm run <script>    # Compact npm script output
rtk npx <cmd>           # Compact npx command output
rtk prisma              # Prisma without ASCII art (88%)
rtk uv run <cmd>        # Compact uv project command output
```

### Files & Search (60-75% savings)
```bash
rtk ls <path>           # Tree format, compact (65%)
rtk read <file>         # Code reading with filtering (60%)
rtk grep <pattern>      # Search grouped by file (75%). Format flags (-c, -l, -L, -o, -Z) run raw.
rtk find <pattern>      # Find grouped by directory (70%)
```

### Analysis & Debug (70-90% savings)
```bash
rtk err <cmd>           # Filter errors only from any command
rtk log <file>          # Deduplicated logs with counts
rtk json <file>         # JSON structure without values
rtk deps                # Dependency overview
rtk env                 # Environment variables compact
rtk summary <cmd>       # Smart summary of command output
rtk diff                # Ultra-compact diffs
```

### Infrastructure (85% savings)
```bash
rtk docker ps           # Compact container list
rtk docker images       # Compact image list
rtk docker logs <c>     # Deduplicated logs
rtk kubectl get         # Compact resource list
rtk kubectl logs        # Deduplicated pod logs
```

### Network (65-70% savings)
```bash
rtk curl <url>          # Compact HTTP responses (70%)
rtk wget <url>          # Compact download output (65%)
```

### Meta Commands
```bash
rtk gain                # View token savings statistics
rtk gain --history      # View command history with savings
rtk discover            # Analyze Claude Code sessions for missed RTK usage
rtk proxy <cmd>         # Run command without filtering (for debugging)
rtk init                # Add RTK instructions to CLAUDE.md
rtk init --global       # Add RTK to ~/.claude/CLAUDE.md
```

## Token Savings Overview

| Category | Commands | Typical Savings |
|----------|----------|-----------------|
| Tests | vitest, playwright, cargo test | 90-99% |
| Build | next, tsc, lint, prettier | 70-87% |
| Git | status, log, diff, add, commit | 59-80% |
| GitHub | gh pr, gh run, gh issue | 26-87% |
| Package Managers | pnpm, npm, npx | 70-90% |
| Files | ls, read, grep, find | 60-75% |
| Infrastructure | docker, kubectl | 85% |
| Network | curl, wget | 65-70% |

Overall average: **60-90% token reduction** on common development operations.
<!-- /rtk-instructions -->