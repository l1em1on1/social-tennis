#!/usr/bin/env bash
#
# sandbox.sh — create the project's Docker Sandbox if it doesn't exist, then run it.
# Runs on the HOST. Needs `docker`, `sbx`, and `gh` (logged in).
#
#   ./sandbox.sh            create if missing, update Claude Code, then attach
#   ./sandbox.sh --rebuild  rebuild the template image first
#
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

NAME=social-tennis
TEMPLATE=social-tennis-sbx
KITS=(
  ./.sbx/kits/context7
)

REBUILD=false
for arg in "$@"; do
  case "$arg" in
    --rebuild) REBUILD=true ;;
    *) echo "unknown option: $arg" >&2; exit 2 ;;
  esac
done

# --- secrets: stored globally in sbx, so they survive rebuilds -------------
# Filtered by service so a match in another secret's host (mcp.context7.com)
# doesn't count.
have_secret() { sbx secret ls -g --service "$1" 2>/dev/null | grep -qw "$1"; }

have_secret github || sbx secret set -g github -t "$(gh auth token)"

# Context7 key, bound to the kit's `service: context7` credential
# (.sbx/kits/context7/spec.yaml). Store the raw key: the kit's `format:
# Bearer %s` adds the prefix. Prompted (hidden) and piped on stdin, so the key
# never sits in a shell profile, history, or process args. Asked when missing,
# and on --rebuild to allow rotation — Enter keeps the stored key.
if ! have_secret context7 || $REBUILD; then
  have_secret context7 && keep=" (Enter keeps the stored key)" || keep=""
  read -rsp "Context7 API key (ctx7sk-...)${keep}: " ctx7_key; echo
  if [[ -n "$ctx7_key" ]]; then
    [[ -n "$keep" ]] && sbx secret rm -g context7 -f
    printf '%s' "$ctx7_key" | sbx secret set -g context7
  elif [[ -z "$keep" ]]; then
    echo "A Context7 API key is required." >&2; exit 1
  fi
  unset ctx7_key
fi

# --- template: build + load if missing (sbx has its own image store) -------
if $REBUILD || ! sbx template ls 2>/dev/null | grep -qw "$TEMPLATE"; then
  docker build -t "$TEMPLATE" .sbx
  tar=$(mktemp).tar
  docker save "$TEMPLATE" -o "$tar"
  sbx template load "$tar"
  rm -f "$tar"
fi

# --- sandbox: create if missing, then run ---------------------------------
if ! sbx ls 2>/dev/null | grep -qw "$NAME"; then
  kit_args=(); for k in "${KITS[@]}"; do kit_args+=(--kit "$k"); done
  sbx create --name "$NAME" -t "$TEMPLATE" "${kit_args[@]}" claude .
fi

# Update Claude Code on every run: the sandbox otherwise stays at whatever
# version the template shipped. `claude update` picks the right path for however
# the base image installed it. A failed update (e.g. offline) only warns, so it
# never blocks attaching.
sbx exec -it "$NAME" claude update ||
  echo "warning: claude update failed; continuing with the installed version" >&2

exec sbx run --name "$NAME"
