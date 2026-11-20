---
status: accepted
---

# CI: checks that always report, pinned by digest and SHA, and Claude's review on request

The CI workflows are described in `docs/architecture.md`, "CI". This records why they work that way.

## Every check reports on every PR

- **The check workflows have no `paths` filter.** GitHub leaves the checks of a path-skipped workflow *pending*, and once those checks are required, that blocks a docs-only PR. So each job is skipped with `if`, which reports as skipped and satisfies a required check. This replaces ADR-0002's path-based triggers.
- **A job runs unless `changed paths` says there's nothing to do.** A failed or cancelled detector then runs everything, so it can't let a required check pass by skipping it.
- **A skipped e2e run still reports to Chromatic.** Chromatic posts its "UI Tests" checks only when a build is published or skipped, so a `chromatic-skip` job skips a build for each project with a token.

## Pinned

- **Actions by commit SHA,** with the version in a comment, so a moved tag can't change what runs.
- **Trivy by digest.** Trivy's own release channels were compromised once (GHSA-69fq-xp46-6x23), so a tag alone isn't trusted.

## Claude's review

- **Only on request**, when the owner adds the `claude-review` label. It bills the owner's Claude subscription, never API credits.
- **Advice, not a gate.** Its check isn't required. A run that fails (a usage limit, a rejected token) ends green, with a comment giving the error.
- **Line comments only on changed lines.** GitHub accepts line comments only within the diff, so every other finding goes in the summary comment.
- **Earlier line comments are written to `pr-context/` first.** `gh pr view --comments` doesn't show them, and without them a re-review would repeat open findings.
- **Only usage numbers are read from the run.** The action's `display_report` would publish the whole session on this public repository.
- **It runs on `pull_request_target`.** The workflow and Claude's configuration then always come from `main`, and no other branch's workflow can read the token. The PR's files are checked out without credentials for Claude to read.
