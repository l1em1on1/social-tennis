#!/usr/bin/env bash
# WorktreeCreate hook (.claude/settings.json, #185). It replaces Claude Code's
# own worktree creation, and does the same: a new branch from origin/main, in
# .claude/worktrees/<name>. Then it starts the language servers' restores for
# that worktree in the background, as the SessionStart hook does for the main
# checkout, so LSP works there with no manual step. It prints the worktree's
# path, which is how Claude Code finds it; everything else goes to stderr.
set -euo pipefail

repo="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "$0")/../.." && pwd)}"
name=$(jq -r '.name // empty')
if [[ ! $name =~ ^[A-Za-z0-9._-]+(/[A-Za-z0-9._-]+)*$ || $name == *..* ]]; then
  echo "worktree-create: unusable worktree name '$name'" >&2
  exit 1
fi
dir="$repo/.claude/worktrees/$name"

git -C "$repo" fetch --quiet origin main >&2 || echo "worktree-create: fetch failed; using the last origin/main" >&2
# A branch left by an earlier worktree of this name (the remove hook keeps it,
# as Claude Code's own worktrees do) is checked out again rather than failing.
if git -C "$repo" show-ref --verify --quiet "refs/heads/worktree-$name"; then
  git -C "$repo" worktree add "$dir" "worktree-$name" >&2
else
  git -C "$repo" worktree add -b "worktree-$name" "$dir" origin/main >&2
fi

# Its own compose project: by default compose names one after the directory,
# so fix/api and feat/api, or a worktree named like the main checkout, would
# share containers and volumes. Compose reads this from the worktree's .env.
# The readable part loses case and punctuation (fix/api and fix.api read the
# same), so a checksum of the exact name keeps it unique.
project="social-tennis-wt-$(tr 'A-Z' 'a-z' <<<"$name" | tr -c 'a-z0-9_\n-' '-')-$(printf %s "$name" | cksum | cut -d' ' -f1)"
printf 'COMPOSE_PROJECT_NAME=%s\n' "$project" >>"$dir/.env"

# Detached, so they outlive this hook. lsp-restore.sh logs to its own file.
for part in dotnet npm; do
  setsid nohup bash "$repo/.claude/hooks/lsp-restore.sh" "$part" "$dir" </dev/null >/dev/null 2>&1 &
done

echo "$dir"
