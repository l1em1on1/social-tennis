# Issue tracker: GitHub

Issues, specs and PRs live on GitHub (`l1em1on1/social-tennis`). Use `gh` from inside the clone; it infers the repo.

- **Read** an issue: `gh issue view <n> --comments`. A bare `#42` may be an issue or a PR (one number space): try `gh pr view 42`, then `gh issue view 42`.
- **Create**, **comment**, **label**, **close**: `gh issue create|comment|edit --add-label|close`, with a heredoc for multi-line text.
- **Edit a body:** fetch it from the repo directory to a file, check the file isn't empty, then `gh issue edit <n> --body-file <file>`. For a PR body, `gh api -X PATCH repos/l1em1on1/social-tennis/pulls/<n> -F body=@<file>`: `gh pr edit` needs a `read:org` scope the agent's token lacks.

## Pushing to a PR

- **Rebase onto the latest `main` before every push:** `git fetch origin main && git rebase origin/main`, then push (force with lease if the rebase rewrote commits). A PR's Chromatic builds then compare against `main` as it is. An older base can hide a Dependabot merge's visual changes until merging accepts them unseen (ADR-0015).

## For the skills

- **PRs as a request surface: no.** (`/triage` reads this flag.)
- "Publish to the issue tracker": create a GitHub issue.
- "Fetch the relevant ticket": `gh issue view <n> --comments`.

## Wayfinding operations

Used by `/wayfinder`. The **map** is one issue; its **children** are the tickets.

- **Map**: an issue labelled `wayfinder:map`, with a Notes / Decisions-so-far / Fog body (`gh issue create --label wayfinder:map`).
- **Child ticket**: a GitHub sub-issue of the map (`gh api`, sub-issues endpoint), labelled `wayfinder:<type>` (`research`, `prototype`, `grilling`, `task`). Without sub-issues: a task list in the map body, and `Part of #<map>` atop the child. A claimed ticket is assigned to the driving dev.
- **Blocking**: GitHub's native issue dependencies: `gh api --method POST repos/<owner>/<repo>/issues/<child>/dependencies/blocked_by -F issue_id=<blocker-db-id>`, where the database id is `gh api repos/<owner>/<repo>/issues/<n> --jq .id` (not the number or `node_id`). `issue_dependencies_summary.blocked_by` counts open blockers. Without dependencies: a `Blocked by: #<n>, #<n>` line atop the child. A ticket is unblocked when every blocker is closed.
- **Frontier**: the map's open children with no open blocker and no assignee; the first in map order wins.
- **Claim**: `gh issue edit <n> --add-assignee @me`, the session's first write.
- **Resolve**: comment the answer, close the ticket, then append a pointer (gist and link) to the map's Decisions-so-far.
