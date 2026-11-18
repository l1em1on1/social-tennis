#!/usr/bin/env bash
# PreToolUse hook on Bash for the read-only subagents in .claude/agents/
# (ADR-0023). They read and report; only the main session changes files,
# git or GitHub. Exit 2 blocks the command and tells the subagent why.
#
# A guardrail against mistakes, not a sandbox: it matches command text.
# Tests: bash .claude/hooks/subagent-guard.test.sh
set -u

deny() {
  echo "Blocked for subagents (ADR-0023): $1. Report what you need to the main session instead." >&2
  exit 2
}

# Fail closed: without the command there is nothing to check.
command -v jq >/dev/null 2>&1 || deny "jq is missing, so the guard can't read the command"
raw=$(jq -r '.tool_input.command // empty') || deny "the guard couldn't read the command"

# Quoted strings are emptied, so grep "git push" or jq 'select(.n > 1)' isn't
# read as a command. docker-compose is read as docker compose.
cmd=$(sed -E "s/'[^']*'/''/g; s/\"[^\"]*\"/\"\"/g; s/docker-compose/docker compose/g" <<<"$raw")

# A command word: at the start or after ; & | ( $( or a backtick, after any
# wrappers (sudo, env, command, exec, nice, timeout, xargs), with any path.
wrap='((sudo|command|exec|env([[:space:]]+[A-Za-z_][A-Za-z0-9_]*=[^[:space:]]*)*|nice([[:space:]]+-n)?([[:space:]]+-?[0-9]+)?|timeout([[:space:]]+-[^[:space:]]+)*[[:space:]]+[0-9.]+[smhd]?|xargs([[:space:]]+-[^[:space:]]+)*)[[:space:]]+)*'
at='(^|[;&|(`]|\$\()[[:space:]]*'"$wrap"'([^[:space:];&|()]*/)?'
# Each invocation of a tool, up to the next ; & | or ).
calls() { grep -oE "${at}$1([[:space:]][^;&|()]*)?([;&|()]|$)" <<<"$cmd" | sed -E "s/[;&|()]$//"; }
# Options before a subcommand. git's long options take =value only (so
# `git --no-pager push` is still read as push); docker's may take a value.
gopts='([[:space:]]+(--[a-z-]+(=[^[:space:]]*)?|-[a-zA-Z]+([[:space:]]+[^-[:space:]][^[:space:]]*)?))*'
dopts='([[:space:]]+(--[a-z-]+((=|[[:space:]]+)[^-[:space:]][^[:space:]]*)?|-[a-zA-Z]+([[:space:]]+[^-[:space:]][^[:space:]]*)?))*'
# A subcommand: a whole word, then the rest of the call.
word='([a-z][a-z-]*)([[:space:]].*)?$'
# After a match against ...${word}: the subcommand and the rest.
sub_rest() { local n=${#BASH_REMATCH[@]}; sub=${BASH_REMATCH[n - 2]}; rest=${BASH_REMATCH[n - 1]}; }

# Shells and eval would hide the command from every check below.
if grep -qE "${at}((ba|da|z|k)?sh[[:space:]]+-[a-zA-Z]*c|eval)([[:space:]]|$)" <<<"$cmd"; then
  deny "commands run through sh -c or eval"
fi

# git: reading only. fetch is allowed: it updates remote-tracking refs only.
while IFS= read -r call; do
  [[ $call =~ git${gopts}[[:space:]]*$ ]] && continue # only options: --version, --help
  [[ $call =~ git${gopts}[[:space:]]+${word} ]] || deny "a git command the guard can't read"
  sub_rest
  case "$sub" in
    push | commit | merge | rebase | reset | checkout | switch | restore | clean | add | rm | mv | cherry-pick | \
    revert | am | pull | update-ref | update-index | gc | notes | submodule | prune | repack | replace | \
    filter-branch | init | clone | symbolic-ref)
      deny "git $sub" ;;
    stash) [[ $rest =~ ^[[:space:]]+(list|show)([[:space:]]|$) ]] || deny "git stash (only list and show)" ;;
    worktree) [[ $rest =~ ^[[:space:]]+list([[:space:]]|$) ]] || deny "git worktree (only list)" ;;
    apply) [[ $rest =~ --(check|stat|numstat|summary) ]] || deny "git apply (only --check or --stat)" ;;
    config) [[ $rest =~ --(get|get-all|get-regexp|list|show-origin)|(^|[[:space:]])-l([[:space:]]|$) ]] || deny "git config (only reading)" ;;
    remote) [[ $rest =~ ^[[:space:]]+(add|remove|rm|rename|set-url|set-head|set-branches|prune|update)([[:space:]]|$) ]] && deny "git remote changes" ;;
    tag)
      [[ $rest =~ ^[[:space:]]*$ || $rest =~ ^[[:space:]]+(-l|--list|-n[0-9]*|--contains|--no-contains|--points-at|--merged|--no-merged|--sort|--format)([[:space:]=]|$) ]] \
        || deny "git tag (only listing)" ;;
    branch)
      if [[ $rest =~ ^[[:space:]]+[^-[:space:]] || $rest =~ (^|[[:space:]])(-[a-zA-Z]*[dDmMcCf][a-zA-Z]*|--(delete|move|copy|force|set-upstream-to|unset-upstream|edit-description))([[:space:]=]|$) ]]; then
        deny "git branch changes"
      fi ;;
  esac
done < <(calls git)

# gh: reading subcommands only, and `gh api` only as GET or a GraphQL query.
while IFS= read -r call; do
  call=$(sed -E 's/[[:space:]](-R|--repo)([[:space:]]+|=)[^[:space:]]+//g' <<<"$call")
  [[ $call =~ gh${gopts}[[:space:]]*$ ]] && continue # only options: --version, --help
  # The trailing space lets a verb at the very end match (bash's regex won't take $ in the group).
  [[ "$call " =~ gh[[:space:]]+([a-z][a-z-]*)([[:space:]]+([a-z][a-z-]*)[[:space:]])?(.*)$ ]] \
    || deny "a gh command the guard can't read"
  sub=${BASH_REMATCH[1]} verb=${BASH_REMATCH[3]} rest=${BASH_REMATCH[4]}
  case "$sub $verb" in
    "pr view" | "pr diff" | "pr checks" | "pr list" | "pr status" | "issue view" | "issue list" | "issue status" | \
    "run view" | "run list" | "run watch" | "workflow list" | "workflow view" | "release list" | "release view" | \
    "repo view" | "auth status" | "label list" | "cache list" | "search "*) ;;
    "api graphql")
      grep -qiE "mutation" <<<"$raw" && deny "gh api graphql mutations" ;;
    "api "*)
      args="$verb $rest"
      grep -qE -- "(-X|--method)[[:space:]=]*(POST|PATCH|PUT|DELETE)" <<<"$args" && deny "gh api writes"
      if grep -qE -- "(^|[[:space:]])(-f|-F|--field|--raw-field|--input)([[:space:]=]|$)" <<<"$args" \
         && ! grep -qE -- "(-X|--method)[[:space:]=]*GET" <<<"$args"; then
        deny "gh api with fields (a POST unless -X GET)"
      fi ;;
    *) deny "gh $sub $verb (only reading is allowed)" ;;
  esac
done < <(calls gh)

# docker: logs and state only.
while IFS= read -r call; do
  [[ $call =~ docker${dopts}[[:space:]]*$ ]] && continue # only options: --version, --help
  [[ $call =~ docker${dopts}[[:space:]]+${word} ]] || deny "a docker command the guard can't read"
  sub_rest
  case "$sub" in
    logs | ps | inspect | images | version | info | stats | top) ;;
    container | image | volume | network)
      [[ $rest =~ ^[[:space:]]+(ls|list|inspect|logs|top|stats)([[:space:]]|$) ]] || deny "docker $sub changes" ;;
    compose)
      [[ $rest =~ ^${dopts}[[:space:]]+${word} ]] || deny "a docker compose command the guard can't read"
      sub_rest
      case "$sub" in logs | ps | config | images | top | version | ls) ;; *) deny "docker compose $sub (only logs and state)" ;; esac ;;
    *) deny "docker $sub (only logs and state)" ;;
  esac
done < <(calls docker)

# Files, packages and builds: no deleting, moving, writing, installing or
# building (that runs in compose anyway: CLAUDE.md, ADR-0005).
if grep -qE "${at}(rm|rmdir|mv|cp|tee|truncate|chmod|chown|ln|mkdir|touch|dd|install|shred|unlink|rsync|npm|npx|pnpm|yarn|dotnet|node|python3?|ruby|php)([[:space:]]|$)" <<<"$cmd"; then
  deny "commands that change files, install or build (use jq to read JSON)"
fi
if grep -qE "${at}(sed|perl)[[:space:]]+([^;&|]*[[:space:]])?-[a-zA-Z]*i" <<<"$cmd"; then
  deny "in-place edits"
fi
if grep -qE "${at}find[[:space:]][^;&|]*[[:space:]]-(delete|exec|execdir|ok|okdir|fprint[f0]?|fls)([[:space:]]|$)" <<<"$cmd"; then
  deny "find with -delete, -exec or a file output"
fi
# Redirection to a file; /dev/null and &N (another descriptor) are fine.
while IFS= read -r redirect; do
  target=$(sed -E 's/^(&>>?|[0-9]*>>?\|?)[[:space:]]*//' <<<"$redirect")
  case "$target" in "" | /dev/null | "&"*) ;; *) deny "writing to $target" ;; esac
done < <(grep -oE '(&>>?|[0-9]*>>?\|?)[[:space:]]*(&[0-9-]+|[^[:space:]|;&()<>]+)?' <<<"$cmd")

exit 0
