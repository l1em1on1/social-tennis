#!/usr/bin/env bash
#
# sandbox.sh — create the project's Docker Sandbox if it doesn't exist, then run it.
# Runs on the HOST. Needs `docker`, `sbx`, and `gh` (logged in).
#
#   ./sandbox.sh            create if missing, then attach
#   ./sandbox.sh --rebuild  rebuild the template image first
#
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

NAME=social-tennis
TEMPLATE=social-tennis-sbx
KITS=(
  ./.sbx/kits/context7
)

# --- secrets: set globally in sbx if not already stored ---------------------
have_secret() { sbx secret ls 2>/dev/null | grep -qw "$1"; }

have_secret github || sbx secret set -g github -t "$(gh auth token)"

have_secret CONTEXT7_API_KEY || sbx secret set-custom -g \
  --host mcp.context7.com --env CONTEXT7_API_KEY \
  --value "Bearer ${CONTEXT7_API_KEY:?export CONTEXT7_API_KEY=ctx7sk-... first}"

# --- template: build + load if missing (sbx has its own image store) -------
if [[ "${1:-}" == --rebuild ]] || ! sbx template ls 2>/dev/null | grep -qw "$TEMPLATE"; then
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

exec sbx run --name "$NAME"
