#!/usr/bin/env bash
#
# sandbox.sh — run the project's Docker Sandbox, creating it (and its template)
# on first use. Wraps the manual steps in .sbx/README.md.
#
# Usage:
#   ./sandbox.sh                     # ensure template + sandbox exist, then attach
#   ./sandbox.sh --rebuild           # rebuild + reload the template image first
#   ./sandbox.sh -- claude "prompt"  # pass a command through to `sbx run`
#
# Runs on the HOST (needs `sbx` and `docker`); nothing here touches the project
# toolchain directly — that all lives inside the sandbox.

set -euo pipefail

SBX_NAME="social-tennis"
TEMPLATE="social-tennis-sbx"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SBX_DIR="$SCRIPT_DIR/.sbx"
KIT_DIR="$SBX_DIR/kits/context7"

REBUILD=0
RUN_ARGS=()
while [[ $# -gt 0 ]]; do
  case "$1" in
    --rebuild) REBUILD=1; shift ;;
    --) shift; RUN_ARGS=("$@"); break ;;
    -h|--help)
      sed -n '3,12p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
      exit 0 ;;
    *) echo "sandbox.sh: unknown argument '$1' (use -- to pass args to sbx run)" >&2; exit 2 ;;
  esac
done

die() { echo "sandbox.sh: $*" >&2; exit 1; }

command -v docker >/dev/null 2>&1 || die "docker not found on PATH"
command -v sbx    >/dev/null 2>&1 || die "sbx not found on PATH — install Docker Sandboxes"

if [[ -z "${CONTEXT7_API_KEY:-}" ]]; then
  echo "sandbox.sh: warning — CONTEXT7_API_KEY is not set; the Context7 MCP kit" >&2
  echo "            will fail to authenticate. See .sbx/README.md." >&2
fi

# --- template ---------------------------------------------------------------
if [[ "$REBUILD" -eq 1 ]] || ! sbx template ls 2>/dev/null | grep -qw "$TEMPLATE"; then
  echo "==> Building template image '$TEMPLATE'"
  docker build -t "$TEMPLATE:latest" "$SBX_DIR"

  tar="$(mktemp -t "${TEMPLATE}.XXXXXX.tar")"
  trap 'rm -f "$tar"' EXIT
  echo "==> Exporting and loading into the sandbox image store"
  docker save "$TEMPLATE:latest" -o "$tar"
  sbx template load "$tar"
  rm -f "$tar"
  trap - EXIT
else
  echo "==> Template '$TEMPLATE' already loaded"
fi

# --- sandbox ---------------------------------------------------------------
if sbx ls 2>/dev/null | grep -qw "$SBX_NAME"; then
  echo "==> Sandbox '$SBX_NAME' exists"
else
  echo "==> Creating sandbox '$SBX_NAME'"
  sbx create --name "$SBX_NAME" -t "$TEMPLATE" --kit "$KIT_DIR" claude "$SCRIPT_DIR"
fi

# --- run -----------------------------------------------------------------
echo "==> sbx run --name $SBX_NAME ${RUN_ARGS[*]:-}"
exec sbx run --name "$SBX_NAME" "${RUN_ARGS[@]}"
