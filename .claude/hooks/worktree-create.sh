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
git -C "$repo" worktree add -b "worktree-$name" "$dir" origin/main >&2

# Detached, so they outlive this hook. lsp-restore.sh logs to its own file.
for part in dotnet npm; do
  setsid nohup bash "$repo/.claude/hooks/lsp-restore.sh" "$part" "$dir" </dev/null >/dev/null 2>&1 &
done

echo "$dir"
