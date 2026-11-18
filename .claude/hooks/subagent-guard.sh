#!/usr/bin/env bash
# PreToolUse hook on Bash for the read-only subagents in .claude/agents/
# (ADR-0023). They read and report; only the main session changes files,
# git or GitHub. Exit 2 blocks the command and tells the subagent why.
#
# A guardrail against mistakes, not a sandbox: it matches command text.
set -u

# Quoted strings are emptied first, so a pattern like grep "git push" or
# jq 'select(.n > 1)' isn't read as a command.
command=$(jq -r '.tool_input.command // empty' | sed -E "s/'[^']*'/''/g; s/\"[^\"]*\"/\"\"/g")

deny() {
  echo "Blocked for subagents (ADR-0023): $1. Report what you need to the main session instead." >&2
  exit 2
}

# A command word at the start, or after ; & | ( or a backtick.
at='(^|[;&|(`]|\$\()[[:space:]]*'

# git: reading only.
if grep -qE "${at}git([[:space:]]+-[^[:space:]]+([[:space:]]+[^-[:space:]][^[:space:]]*)?)*[[:space:]]+(push|commit|merge|rebase|reset|checkout|switch|restore|stash|tag|clean|add|rm|mv|cherry-pick|revert|am|apply|pull|worktree|update-ref|branch[[:space:]]+-[dDmMcC])([[:space:]]|$)" <<<"$command"; then
  deny "git commands that change the repository"
fi

# gh: reading subcommands only, and `gh api` only as GET.
while read -r sub verb; do
  case "$sub $verb" in
    "pr view" | "pr diff" | "pr checks" | "pr list" | "pr status" | \
    "issue view" | "issue list" | "issue status" | \
    "run view" | "run list" | "run watch" | "search "* | "api "*) ;;
    *) deny "gh $sub $verb (only reading is allowed)" ;;
  esac
done < <(grep -oE "${at}gh[[:space:]]+[a-z-]+([[:space:]]+[a-z-]+)?" <<<"$command" | sed -E "s/^.*gh[[:space:]]+//")
if grep -qE "${at}gh[[:space:]]+api([[:space:]]|$)" <<<"$command"; then
  if grep -qE -- "(-X|--method)[[:space:]=]*(POST|PATCH|PUT|DELETE)" <<<"$command"; then
    deny "gh api writes"
  fi
  # Fields or --input make gh api POST unless the method is GET.
  if grep -qE -- "[[:space:]](-f|-F|--field|--raw-field|--input)([[:space:]=]|$)" <<<"$command" \
     && ! grep -qE -- "(-X|--method)[[:space:]=]*GET" <<<"$command"; then
    deny "gh api with fields (a POST unless -X GET)"
  fi
fi

# docker: logs and state only.
while read -r sub; do
  case "$sub" in
    logs | ps | inspect | images | version | info | "compose logs" | "compose ps" | "compose config" | "compose images") ;;
    *) deny "docker $sub (only logs and state are allowed)" ;;
  esac
done < <(grep -oE "${at}docker[[:space:]]+(compose([[:space:]]+-[^[:space:]]+([[:space:]]+[^-[:space:]][^[:space:]]*)?)*[[:space:]]+)?[a-z-]+" <<<"$command" \
  | sed -E "s/^.*docker[[:space:]]+//; s/^compose([[:space:]]+-[^[:space:]]+([[:space:]]+[^-[:space:]][^[:space:]]*)?)*[[:space:]]+/compose /")

# Files: no deleting, moving, in-place editing or writing.
if grep -qE "${at}(rm|rmdir|mv|cp|tee|truncate|chmod|chown|ln|mkdir|touch|dd)([[:space:]]|$)" <<<"$command" \
   || grep -qE "${at}sed[[:space:]]+([^|;&]*[[:space:]])?-[a-zA-Z]*i" <<<"$command"; then
  deny "commands that change files"
fi
# Redirection to a file; /dev/null and &N are fine.
while read -r target; do
  case "$target" in /dev/null | "&"*) ;; *) deny "writing to $target" ;; esac
done < <(grep -oE "(^|[^0-9&<>-])[0-9]?>>?[[:space:]]*[^[:space:]|;&)]+" <<<"$command" | sed -E 's/^.*>>?[[:space:]]*//')

exit 0
