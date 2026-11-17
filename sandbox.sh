#!/usr/bin/env bash
#
# sandbox.sh — create the project's Docker Sandbox if it doesn't exist, then run it.
# Runs on the HOST. Needs `sbx` 0.51 or later. Options: see usage() or --help.
#
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

NAME=social-tennis
# v3 kits (#175): sbx 0.51 made the built-in `claude` agent a v3 kit, and an
# agent's kits must all be one format. The toolchain kit replaces the custom
# template of the v2 setup.
KITS=(
  ./.sbx/kits/toolchain
  ./.sbx/kits/context7
  ./.sbx/kits/github
  ./.sbx/kits/nuget
  ./.sbx/kits/rtk
)
SBX_MIN_VERSION=0.51.0
# The Buildx builder sbx builds the kits with (see "sandbox" below).
KIT_BUILDER=sbx-kit-builder

SECRETS=(github context7)

usage() {
  local services; services=$(printf ' | %s' "${SECRETS[@]}"); services=${services:3}
  cat <<USAGE
Usage: ./sandbox.sh [--name NAME] [--debug] [--no-remote-control] [--set-secret SERVICE] [--help]

Create the "$NAME" Docker Sandbox if it doesn't exist, update Claude
Code in it, then attach with Remote Control on. Runs on the host; needs
sbx $SBX_MIN_VERSION or later.

Options:
  --name NAME           use the sandbox NAME instead of "$NAME", e.g. to
                        create a new one beside an existing sandbox
  --debug               create with sbx's debug output (\`sbx -D\`): each kit
                        install hook's output, and the startup log
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

REMOTE_CONTROL=true
SBX_FLAGS=()
SET_SECRET=""
while (($#)); do
  case "$1" in
    -h|--help) usage; exit 0 ;;
    --name)
      NAME=${2:-}
      [[ -n "$NAME" ]] || { echo "--name needs a sandbox name" >&2; exit 2; }
      shift ;;
    --no-remote-control) REMOTE_CONTROL=false ;;
    --debug) SBX_FLAGS=(-D) ;;
    --set-secret)
      SET_SECRET=${2:-}
      secret_label "$SET_SECRET" >/dev/null || exit 2
      shift ;;
    *) echo "unknown option: $1" >&2; usage >&2; exit 2 ;;
  esac
  shift
done

# --- sbx version: v3 kits need 0.51 ------------------------------------------
# An older sbx reads the kits' schemaVersion "3" as unknown and refuses them.
# If the version can't be read, the script goes on and sbx has the last word.
sbx_version=$(sbx version 2>/dev/null | grep -oE '[0-9]+\.[0-9]+\.[0-9]+' | head -n 1 || true)
if [[ -z "$sbx_version" ]]; then
  echo "warning: could not read sbx's version; the kits need $SBX_MIN_VERSION or later" >&2
elif [[ "$(printf '%s\n' "$SBX_MIN_VERSION" "$sbx_version" | sort -V | head -n 1)" != "$SBX_MIN_VERSION" ]]; then
  echo "sbx $sbx_version is too old: the kits are v3, which needs sbx $SBX_MIN_VERSION or later." >&2
  exit 1
fi

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

# --- sandbox: create if missing, then run ---------------------------------
# Exact match on the name column: `grep -w` would also match claude-$NAME.
# sbx builds the local kits itself at creation (the toolchain kit's image takes
# a few minutes the first time), so there is no template to build here.
if ! sbx ls 2>/dev/null | awk -v n="$NAME" '$1 == n { found=1 } END { exit !found }'; then
  # sbx builds the kits that have a Dockerfile (toolchain, rtk) with the host's
  # Docker Buildx, exporting OCI images. Buildx's default `docker` driver can't
  # with Docker's classic image store ("OCI exporter is not supported for the
  # docker driver"), so a docker-container builder is created once and used,
  # unless the caller picked a builder (BUILDX_BUILDER, or SBX_KIT_BUILDER).
  # Without Docker, sbx builds in a builder sandbox of its own.
  # https://github.com/docker/sandbox-kit-spec#readme, "The local loop"
  if [[ -z "${BUILDX_BUILDER:-}${SBX_KIT_BUILDER:-}" ]] && command -v docker >/dev/null 2>&1; then
    if docker buildx inspect "$KIT_BUILDER" >/dev/null 2>&1 ||
       docker buildx create --name "$KIT_BUILDER" --driver docker-container --bootstrap >/dev/null; then
      export BUILDX_BUILDER=$KIT_BUILDER
    else
      echo "warning: could not create the $KIT_BUILDER Buildx builder; sbx picks its own" >&2
    fi
  fi
  kit_args=(); for k in "${KITS[@]}"; do kit_args+=(--kit "$k"); done
  sbx ${SBX_FLAGS[@]+"${SBX_FLAGS[@]}"} create --name "$NAME" "${kit_args[@]}" claude .
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

# Update Claude Code on every run: the sandbox otherwise stays at the version
# the claude kit's image carried when it was created. The kit installs it in
# the agent's ~/.local/bin, which `claude update` can write. A failed update
# (e.g. offline) only warns, so it never blocks attaching.
sbx exec -it "$NAME" claude update ||
  echo "warning: claude update failed; continuing with the installed version" >&2

# Arguments after `--` go to Claude Code. sbx keeps its own default flags
# (--dangerously-skip-permissions) when the first one is itself a flag.
if $REMOTE_CONTROL; then
  exec sbx run --name "$NAME" -- --remote-control
fi
exec sbx run --name "$NAME"
