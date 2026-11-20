#!/usr/bin/env bash
# WorktreeRemove hook (.claude/settings.json, #185): removes a worktree that
# worktree-create.sh made. Its compose project (COMPOSE_PROJECT_NAME in its
# .env) has its own containers and volumes, so those go first. The branch stays: it
# may hold commits.
set -u

# Read with bash, not jq (missing in a Windows host's Git Bash); JSON escapes
# a path's backslashes and may escape its slashes.
input=$(cat)
path=""
if [[ $input =~ \"worktree_path\"[[:space:]]*:[[:space:]]*\"(([^\"\\]|\\.)*)\" ]]; then
  path=${BASH_REMATCH[1]}
  path=${path//\\\//\/}
  path=${path//\\\\/\\}
fi
# Claude Code counts the worktree removed when this exits 0, and kept when it
# exits non-zero while the directory is still there.
[[ -n $path ]] || { echo "worktree-remove: no worktree_path in the hook's input" >&2; exit 1; }
# The main checkout, also from a session already in a worktree (where
# CLAUDE_PROJECT_DIR is that worktree), so worktrees sit side by side, never
# one inside another.
here="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "$0")/../.." && pwd)}"
common=$(git -C "$here" rev-parse --path-format=absolute --git-common-dir) && repo=$(cd "$common/.." && pwd) || repo=$here
# Already gone (deleted by hand, say): only git's record of it is left, and
# only that goes (no prune: see worktree-create.sh).
[[ -e $path ]] || { git -C "$repo" worktree remove --force "$path" >/dev/null 2>&1; exit 0; }

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
[[ ! -e $path ]]
