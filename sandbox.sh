#!/usr/bin/env bash
#
# sandbox.sh — create the project's Docker Sandbox if it doesn't exist, then run it.
# Runs on the HOST. Needs `docker` and `sbx`.
#
#   ./sandbox.sh                       create if missing, update Claude Code, then attach
#   ./sandbox.sh --rebuild             rebuild the template image first
#   ./sandbox.sh --set-secret SERVICE  replace one stored secret, then exit
#                                      (SERVICE: github | context7)
#
# Missing secrets are asked for on any run; stored ones are only replaced by
# --set-secret.
#
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

NAME=social-tennis
TEMPLATE=social-tennis-sbx
KITS=(
  ./.sbx/kits/context7
  ./.sbx/kits/github
)

SECRETS=(github context7)

# secret_label SERVICE — the prompt for a secret; fails on an unknown service.
secret_label() {
  case "$1" in
    # Built-in `github` service, used by the `github` kit (gh + git push over
    # HTTPS). A fine-grained PAT scoped to this repo, so the agent gets only
    # what the token grants — see .sbx/README.md for the permissions.
    github)   echo "GitHub fine-grained PAT (github_pat_...)" ;;
    # Bound to the context7 kit's `service: context7` credential. Store the
    # raw key: the kit's `format: Bearer %s` adds the prefix.
    context7) echo "Context7 API key (ctx7sk-...)" ;;
    "") echo "--set-secret needs a service: ${SECRETS[*]}" >&2; return 2 ;;
    *)  echo "unknown secret: $1 (expected: ${SECRETS[*]})" >&2; return 2 ;;
  esac
}

REBUILD=false
SET_SECRET=""
while (($#)); do
  case "$1" in
    --rebuild) REBUILD=true ;;
    --set-secret)
      SET_SECRET=${2:-}
      secret_label "$SET_SECRET" >/dev/null || exit 2
      shift ;;
    *) echo "unknown option: $1" >&2; exit 2 ;;
  esac
  shift
done

# --- secrets: stored globally in sbx, so they survive rebuilds -------------
# Exact match on the SERVICE column (SCOPE SERVICE SECRET): a plain grep also
# matches the "No secrets found for ... service "<name>"" message.
have_secret() {
  sbx secret ls -g --service "$1" 2>/dev/null |
    awk -v n="$1" '$2 == n { found=1 } END { exit !found }'
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
  have_secret "$service" && sbx secret rm -g "$service" -f
  printf '%s' "$value" | sbx secret set -g "$service"
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
  tar=$(mktemp).tar
  docker save "$TEMPLATE" -o "$tar"
  sbx template load "$tar"
  rm -f "$tar"
fi

# --- sandbox: create if missing, then run ---------------------------------
# Exact match on the name column: `grep -w` would also match claude-$NAME.
if ! sbx ls 2>/dev/null | awk -v n="$NAME" '$1 == n { found=1 } END { exit !found }'; then
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
