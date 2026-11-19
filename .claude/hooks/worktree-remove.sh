#!/usr/bin/env bash
# WorktreeRemove hook (.claude/settings.json, #185): removes a worktree that
# worktree-create.sh made. Its compose project (COMPOSE_PROJECT_NAME in its
# .env) has its own containers and volumes, so those go first. The branch stays: it
# may hold commits.
set -u

path=$(jq -r '.worktree_path // empty')
[[ -n $path && -d $path ]] || exit 0
repo="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "$0")/../.." && pwd)}"

# Only with the project name worktree-create.sh wrote: without it, compose
# would fall back to the directory's name, which another checkout may share.
if grep -q '^COMPOSE_PROJECT_NAME=social-tennis-wt-' "$path/.env" 2>/dev/null; then
  # With the dev overlay too, so its web_node_modules volume goes as well.
  (cd "$path" && docker compose -f docker-compose.yml -f docker-compose.dev.yml \
    --profile e2e --profile storybook --profile test down -v --remove-orphans) >&2 2>&1 || true
else
  echo "worktree-remove: no own compose project name in $path/.env; containers and volumes left alone" >&2
fi
git -C "$repo" worktree remove --force "$path" >&2 \
  || echo "worktree-remove: $path is left behind (files a container wrote as root?)" >&2
git -C "$repo" worktree prune >&2
