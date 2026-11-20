#!/usr/bin/env bash
# WorktreeCreate hook (.claude/settings.json, #185). It replaces Claude Code's
# own worktree creation, and does the same: a new branch from origin/main, in
# .claude/worktrees/<name>. Then it starts the language servers' restores for
# that worktree in the background, as the SessionStart hook does for the main
# checkout, so LSP works there with no manual step. It prints the worktree's
# path, which is how Claude Code finds it; everything else goes to stderr.
set -euo pipefail

# The main checkout, also from a session already in a worktree (where
# CLAUDE_PROJECT_DIR is that worktree), so worktrees sit side by side, never
# one inside another.
here="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "$0")/../.." && pwd)}"
common=$(git -C "$here" rev-parse --path-format=absolute --git-common-dir) && repo=$(cd "$common/.." && pwd) || repo=$here
# The one field read with bash, not jq, which a Windows host's Git Bash lacks.
# The allowed characters below exclude quotes and backslashes, so no JSON
# escape can hide in a valid name.
input=$(cat)
[[ $input =~ \"name\"[[:space:]]*:[[:space:]]*\"([^\"]*)\" ]] && name=${BASH_REMATCH[1]} || name=""
if [[ ! $name =~ ^[A-Za-z0-9._-]+(/[A-Za-z0-9._-]+)*$ || $name == *..* ]]; then
  echo "worktree-create: unusable worktree name '$name'" >&2
  exit 1
fi
dir="$repo/.claude/worktrees/$name"

git -C "$repo" fetch --quiet origin main >&2 || echo "worktree-create: fetch failed; using the last origin/main" >&2
# A worktree of this name deleted by hand is still registered, and git would
# refuse to add it again.
git -C "$repo" worktree prune >&2
# Claude Code calls this for a name that exists too (`claude --worktree <name>
# --resume` after keeping one): a linked worktree's .git is a file, and that
# one is reopened as it is.
if [[ ! -f $dir/.git ]]; then
  branch="worktree-$name"
  # `claude --worktree "#<n>"` arrives as pr-<n>: Claude Code's own creation
  # starts that from the pull request's head, so this does too, and brings a
  # branch kept from an earlier one up to it. The head is fetched into a ref of
  # its own (outside refs/remotes, which a prune would clear), so its last value
  # tells a force-pushed PR apart from local commits.
  if [[ $name =~ ^pr-([0-9]+)$ ]]; then
    head="refs/pull/${BASH_REMATCH[1]}/head"
    last=$(git -C "$repo" rev-parse -q --verify "$head" || true)
    git -C "$repo" fetch --quiet origin "+$head:$head" >&2
    if ! git -C "$repo" show-ref --verify --quiet "refs/heads/$branch"; then
      git -C "$repo" branch "$branch" "$head" >&2
    elif [[ $(git -C "$repo" rev-parse "$branch") == "$last" ]] \
      || git -C "$repo" merge-base --is-ancestor "$branch" "$head"; then
      git -C "$repo" branch -f "$branch" "$head" >&2
    else
      echo "worktree-create: $branch has commits the pull request doesn't, so it stays where it is, not at the PR's head" >&2
    fi
  elif ! git -C "$repo" show-ref --verify --quiet "refs/heads/$branch"; then
    git -C "$repo" branch "$branch" origin/main >&2
  fi
  # A branch left by an earlier worktree of this name (the remove hook keeps
  # it, as Claude Code's own worktrees do) is checked out again.
  git -C "$repo" worktree add "$dir" "$branch" >&2
fi

# Its own compose project: by default compose names one after the directory,
# so fix/api and feat/api, or a worktree named like the main checkout, would
# share containers and volumes. Compose reads this from the worktree's .env.
# The readable part loses case and punctuation (fix/api and fix.api read the
# same), so a checksum of the exact name keeps it unique. A reopened worktree
# keeps the name it has, and one made before this hook gets one.
if ! grep -q '^COMPOSE_PROJECT_NAME=' "$dir/.env" 2>/dev/null; then
  project="social-tennis-wt-$(tr 'A-Z' 'a-z' <<<"$name" | tr -c 'a-z0-9_\n-' '-')-$(printf %s "$name" | cksum | cut -d' ' -f1)"
  printf 'COMPOSE_PROJECT_NAME=%s\n' "$project" >>"$dir/.env"
fi

# dotnet first, before the path is returned, as the main checkout's
# SessionStart hook does: seconds with the NuGet cache warm, and csharp-ls
# then never loads against a worktree with no obj/project.assets.json.
bash "$repo/.claude/hooks/lsp-restore.sh" dotnet "$dir" </dev/null >/dev/null 2>&1 || true
# npm takes minutes, so it runs detached and outlives this hook. Both log to
# lsp-restore.sh's own file; neither does anything where the tool is missing.
detach=(nohup)
command -v setsid >/dev/null 2>&1 && detach=(setsid nohup)
"${detach[@]}" bash "$repo/.claude/hooks/lsp-restore.sh" npm "$dir" </dev/null >/dev/null 2>&1 &

echo "$dir"
