# Single repo, not split repos, for web and api

One GitHub repo (`l1em1on1/social-tennis`) with `web/` (Next.js) and `api/` (.NET) as top-level folders, rather than two separate repos. Chosen because API-contract changes and their generated TS client can land in the same commit, and because the project uses a single GitHub Issues tracker rather than splitting or cross-linking two. Not a JS-style monorepo (no Nx/Turborepo) since there's no shared build graph between a dotnet solution and an npm/pnpm project — just path-based CI triggers per folder.

**Amended (2026-11-20): no path-based triggers.** The check workflows run on every PR, and each job skips itself when its folder hasn't changed, so its required check still reports (ADR-0029).
