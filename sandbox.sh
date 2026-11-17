#!/usr/bin/env bash
#
# sandbox.sh — create the project's Docker Sandbox if it doesn't exist, then run it.
# Runs on the HOST. Needs `docker` and `sbx`. Options: see usage() or --help.
#
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

NAME=social-tennis
TEMPLATE=social-tennis-sbx
KITS=(
  ./.sbx/kits/context7
  ./.sbx/kits/github
  ./.sbx/kits/nuget
  ./.sbx/kits/rtk
)

SECRETS=(github context7)

usage() {
  local services; services=$(printf ' | %s' "${SECRETS[@]}"); services=${services:3}
  cat <<USAGE
Usage: ./sandbox.sh [--rebuild] [--no-remote-control] [--set-secret SERVICE] [--help]

Create the "$NAME" Docker Sandbox if it doesn't exist, update Claude
Code in it, then attach with Remote Control on. Runs on the host; needs
docker and sbx.

Options:
  --rebuild             rebuild and reload the template image first
  --no-remote-control   attach without Remote Control, and leave sbx's
                        claude.remoteControl setting as it is
  --set-secret SERVICE  replace one stored secret, then exit
                        (SERVICE: $services)
  -h, --help            show this help, then exit

Missing secrets are asked for on any run; stored ones are only replaced by
--set-secret. Kits attach at creation only: after changing one under
.sbx/kits, run \`sbx rm $NAME\` and then this script again.
USAGE
}

# secret_label SERVICE — the prompt for a secret; fails on an unknown service.
secret_label() {
  case "$1" in
    # Built-in `github` service, used by the `github` kit (gh + git push over
    # HTTPS). A PAT of the l1em1on1-claude account, a write collaborator on
    # this repo only — see .sbx/README.md for the scopes.
    github)   echo "GitHub PAT of the l1em1on1-claude account" ;;
    # Bound to the context7 kit's `service: context7` credential. Store the
    # raw key: the kit's `format: Bearer %s` adds the prefix.
    context7) echo "Context7 API key (ctx7sk-...)" ;;
    "") echo "--set-secret needs a service: ${SECRETS[*]}" >&2; return 2 ;;
    *)  echo "unknown secret: $1 (expected: ${SECRETS[*]})" >&2; return 2 ;;
  esac
}

REBUILD=false
REMOTE_CONTROL=true
SET_SECRET=""
while (($#)); do
  case "$1" in
    -h|--help) usage; exit 0 ;;
    --rebuild) REBUILD=true ;;
    --no-remote-control) REMOTE_CONTROL=false ;;
    --set-secret)
      SET_SECRET=${2:-}
      secret_label "$SET_SECRET" >/dev/null || exit 2
      shift ;;
    *) echo "unknown option: $1" >&2; usage >&2; exit 2 ;;
  esac
  shift
done

# --- secrets: stored globally in sbx, so they survive rebuilds -------------
# Global is sbx's default for secrets since v0.38.0; `-g` is deprecated (#173).
# Exact match on the SCOPE and SERVICE columns (SCOPE SERVICE SECRET): a plain
# grep also matches the "No secrets found for ... service "<name>"" message,
# and without `-g` the list includes secrets scoped to one sandbox.
have_secret() {
  sbx secret ls --service "$1" 2>/dev/null |
    awk -v n="$1" '$1 == "(global)" && $2 == n { found=1 } END { exit !found }'
}

# store_secret SERVICE — ask for a secret (hidden) and store it under SERVICE,
# replacing any stored one. Piped on stdin, so it never sits in a shell
# profile, history, or process args. An empty answer aborts before anything
# stored is removed.
store_secret() {
  local service=$1 label value
  label=$(secret_label "$service")
  read -rsp "${label}: " value; echo
  [[ -n "$value" ]] || { echo "${label} is required." >&2; exit 1; }
  have_secret "$service" && sbx secret rm "$service" -f
  printf '%s' "$value" | sbx secret set "$service"
}

if [[ -n "$SET_SECRET" ]]; then
  store_secret "$SET_SECRET"
  echo "Stored $SET_SECRET."
  exit 0
fi

for s in "${SECRETS[@]}"; do have_secret "$s" || store_secret "$s"; done

# --- template: build + load if missing (sbx has its own image store) -------
if $REBUILD || ! sbx template ls 2>/dev/null | grep -qw "$TEMPLATE"; then
  docker build -t "$TEMPLATE" .sbx
  # A temp dir, not `$(mktemp).tar`: that leaks the file mktemp made. The trap
  # covers a failed save/load under `set -e`; the explicit rm covers success,
  # because the closing `exec` replaces the shell before EXIT traps would run.
  tardir=$(mktemp -d)
  trap 'rm -rf "$tardir"' EXIT
  docker save "$TEMPLATE" -o "$tardir/$TEMPLATE.tar"
  sbx template load "$tardir/$TEMPLATE.tar"
  rm -rf "$tardir"
  trap - EXIT
fi

# --- sandbox: create if missing, then run ---------------------------------
# Exact match on the name column: `grep -w` would also match claude-$NAME.
if ! sbx ls 2>/dev/null | awk -v n="$NAME" '$1 == n { found=1 } END { exit !found }'; then
  kit_args=(); for k in "${KITS[@]}"; do kit_args+=(--kit "$k"); done
  sbx create --name "$NAME" -t "$TEMPLATE" "${kit_args[@]}" claude .
fi

# --- Remote Control: drive the session from claude.ai/code or the phone -----
# sbx keeps Remote Control's credentials on the host, and its proxy hands them
# to a sandbox only while this host-wide setting is on. With it off,
# /remote-control inside the sandbox registers a session that never connects.
# It needs no kit and no extra egress: Remote Control talks to
# api.anthropic.com, which the claude agent is already allowed. A failure
# (e.g. an sbx too old to know the setting) only warns, so it never blocks
# attaching.
# https://docs.docker.com/ai/sandboxes/agents/claude-code/#remote-control
if $REMOTE_CONTROL && [[ "$(sbx settings get claude.remoteControl 2>/dev/null)" != true ]]; then
  sbx settings set claude.remoteControl true ||
    echo "warning: could not turn on sbx's claude.remoteControl; Remote Control will not connect" >&2
fi

# Update Claude Code on every run: the sandbox otherwise stays at whatever
# version the template shipped. `claude update` picks the right path for however
# the base image installed it. A failed update (e.g. offline) only warns, so it
# never blocks attaching.
sbx exec -it "$NAME" claude update ||
  echo "warning: claude update failed; continuing with the installed version" >&2

# Arguments after `--` go to Claude Code. sbx keeps its own default flags
# (--dangerously-skip-permissions) when the first one is itself a flag.
if $REMOTE_CONTROL; then
  exec sbx run --name "$NAME" -- --remote-control
fi
exec sbx run --name "$NAME"
