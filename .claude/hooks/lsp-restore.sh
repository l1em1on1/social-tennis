#!/usr/bin/env bash
# SessionStart hook (.claude/settings.json): keep the packages the language
# servers read up to date (#121). Run as `lsp-restore.sh dotnet` and
# `lsp-restore.sh npm`, two hooks. An optional second argument names another
# checkout, such as a worktree (worktree-create.sh, #185):
#
#   dotnet  synchronous. Seconds once the package cache is warm, and Claude's
#           first reply waits for it, so the first C# lookup sees a current
#           restore. If csharp-ls loaded before a first-ever restore finished,
#           `/reload-plugins` or a new session picks the restore up.
#   npm     async: minutes on the bind mount, and nothing waits for it.
#
#   - C#: csharp-ls resolves packages through each project's
#     obj/project.assets.json. Restored here when one is missing, older than
#     its project file or api/Directory.Build.props, or written for another
#     checkout path.
#   - TypeScript: typescript-language-server reads web/node_modules. Installed
#     here (`npm ci`, scripts off) when missing or older than package-lock.json.
#     On the sandbox's bind mount a full install takes several minutes, which
#     is why the hook is async.
#
# These restores only feed the language servers. Builds, tests and package
# changes still run as compose services (ADR-0005), which keep their own
# output (obj/container, node_modules volumes). Where dotnet or npm isn't
# installed, such as the host, that half does nothing.
set -u

part="${1:-}"
repo="${2:-${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "$0")/../.." && pwd)}}"
log="${TMPDIR:-/tmp}/social-tennis-lsp-restore.log"

# One restore of each kind per checkout at a time, across sessions. flock
# releases if the process dies; children get fd 9 closed so they can't keep
# it held.
if command -v flock >/dev/null 2>&1; then
  exec 9>"${TMPDIR:-/tmp}/social-tennis-lsp-restore-${part}-$(printf %s "$repo" | cksum | cut -d' ' -f1).lock"
  flock -n 9 || exit 0
fi

# stale STAMP INPUT... : STAMP is missing or older than any INPUT.
stale() {
  local stamp="$1" input
  shift
  [ -f "$stamp" ] || return 0
  for input in "$@"; do
    [ "$input" -nt "$stamp" ] && return 0
  done
  return 1
}

if [ "$part" = dotnet ] && command -v dotnet >/dev/null 2>&1; then
  needs_restore=false
  while IFS= read -r project; do
    assets="$(dirname "$project")/obj/project.assets.json"
    if stale "$assets" "$project" "$repo/api/Directory.Build.props" \
       || ! grep -qF "\"projectPath\": \"$project\"" "$assets"; then
      needs_restore=true
    fi
  done < <(find "$repo/api" -name '*.csproj' -not -path '*/obj/*' -not -path '*/bin/*')
  if [ "$needs_restore" = true ]; then
    echo "$(date -u +%FT%TZ) dotnet restore" >>"$log"
    # No node reuse: MSBuild would otherwise leave worker processes behind,
    # holding the lock (fd 9 is closed for them too) and memory.
    if MSBUILDDISABLENODEREUSE=1 dotnet restore "$repo/api/SocialTennis.slnx" -nodeReuse:false \
      >>"$log" 2>&1 9>&-; then
      # A restore with nothing to change leaves project.assets.json as it was,
      # so mark it current, or every session would restore again.
      find "$repo/api" -path '*/obj/project.assets.json' -not -path '*/obj/container/*' -exec touch {} +
    fi
  fi
fi

if [ "$part" = npm ] && command -v npm >/dev/null 2>&1; then
  if stale "$repo/web/node_modules/.package-lock.json" "$repo/web/package-lock.json"; then
    echo "$(date -u +%FT%TZ) npm ci" >>"$log"
    # --no-bin-links: the repo is a virtiofs mount from a Windows host, which
    # refuses symlinks, and the language server never runs node_modules/.bin.
    (cd "$repo/web" && npm ci --ignore-scripts --no-bin-links --no-audit --no-fund) >>"$log" 2>&1 9>&-
  fi
fi

exit 0
