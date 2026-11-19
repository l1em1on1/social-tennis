#!/usr/bin/env bash
# WorktreeRemove hook (.claude/settings.json, #185): removes a worktree that
# worktree-create.sh made. Each worktree directory is its own compose project,
# with its own containers and volumes, so those go first. The branch stays: it
# may hold commits.
set -u

path=$(jq -r '.worktree_path // empty')
[[ -n $path && -d $path ]] || exit 0
repo="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "$0")/../.." && pwd)}"

(cd "$path" && docker compose --profile e2e --profile storybook --profile test down -v --remove-orphans) >&2 2>&1 || true
git -C "$repo" worktree remove --force "$path" >&2 \
  || echo "worktree-remove: $path is left behind (files a container wrote as root?)" >&2
git -C "$repo" worktree prune >&2
