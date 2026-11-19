#!/usr/bin/env bash
# PreToolUse hook on Bash for the read-only subagents in .claude/agents/
# (ADR-0023). They read and report; only the main session changes files,
# git or GitHub. Exit 2 blocks the command and tells the subagent why.
#
# A guardrail against mistakes, not a sandbox: it matches command text.
# Tests: docker compose run --rm guard-tests (CI: .github/workflows/agents.yml)
set -u

deny() {
  echo "Blocked for subagents (ADR-0023): $1. Report what you need to the main session instead." >&2
  exit 2
}

# Fail closed: without the command there is nothing to check.
command -v jq >/dev/null 2>&1 || deny "jq is missing, so the guard can't read the command"
raw=$(jq -r '.tool_input.command // empty') || deny "the guard couldn't read the command"

# Each quoted string becomes the word Q, so grep "git push" or jq
# 'select(.n > 1)' isn't read as a command, while a quoted path still counts
# as a word: `> "$out"` is a redirect to a file, `git -C "$dir" push` a push.
# One left-to-right pass: whichever quote opens first wins, so the apostrophe
# in "Can't" doesn't pair with a later '. docker-compose is read as docker
# compose.
cmd=$(sed -E "s/'[^']*'|\"[^\"]*\"/Q/g; s/docker-compose/docker compose/g" <<<"$raw")

# A command word: at the start or after ; & | ( $( or a backtick, after any
# shell keywords (if, then, do, ..., !, {), VAR=value assignments and wrappers
# (sudo, env, command, exec, nice, timeout, xargs, and rtk with its
# proxy/test/err/bypass), whose options may take a value, with any path.
keys='((if|then|else|elif|do|while|until|time|!|\{)[[:space:]]+)*'
opt='([[:space:]]+(-[a-zA-Z0-9][^[:space:]]*([[:space:]]+[^-=[:space:]][^=[:space:]]*)?|--[a-z-]+(=[^[:space:]]*)?))'
assign='[A-Za-z_][A-Za-z0-9_]*=[^[:space:]]*'
wrap='(('"$assign"'|sudo'"$opt"'*|command|exec|env('"$opt"'|[[:space:]]+'"$assign"')*|nice([[:space:]]+-n[[:space:]]*-?[0-9]+|[[:space:]]+-[0-9]+)?|timeout'"$opt"'*[[:space:]]+[0-9.]+[smhd]?|xargs'"$opt"'*|rtk([[:space:]]+(proxy|test|err|bypass))?)[[:space:]]+)*'
at='(^|[;&|(`]|\$\()[[:space:]]*'"$keys$wrap"'([^[:space:];&|()]*/)?'
# Each invocation of a tool, up to the next ; & | or ). Every separator ends
# its own line first: grep -o consumes the separator after one call, and the
# next call would then have nothing before it to match `at`.
split=$(sed -E 's/([;&|(`])/\1\n/g' <<<"$cmd")
# Each call is returned from its tool name on (the prefix before it is cut),
# so every parse below is anchored with ^ to the tool, not to a later word
# that merely ends in git, gh or docker (r.git, legit, high).
calls() { grep -oE "${at}$1([[:space:]][^;&|()]*)?([;&|()]|$)" <<<"$split" | sed -E "s/[;&|()]$//; s#^${at}##"; }
# Options before a subcommand. git's long options take =value only, and of
# its short ones only -C and -c take a value, so `git --no-pager push` and
# `git -P push` are still read as push. docker's options may take a value.
gopts='([[:space:]]+(--[a-z-]+(=[^[:space:]]*)?|-[Cc][[:space:]]+[^[:space:]]+|-[abd-zABD-Z]+))*'
dopts='([[:space:]]+(--[a-z-]+((=|[[:space:]]+)[^-[:space:]][^[:space:]]*)?|-[a-zA-Z]+([[:space:]]+[^-[:space:]][^[:space:]]*)?))*'
# A subcommand: a whole word, then the rest of the call.
word='([a-z][a-z-]*)([[:space:]].*)?$'
# After a match against ...${word}: the subcommand and the rest.
sub_rest() { local n=${#BASH_REMATCH[@]}; sub=${BASH_REMATCH[n - 2]}; rest=${BASH_REMATCH[n - 1]}; }

# Shells and eval would hide the command from every check below.
if grep -qE "${at}((ba|da|z|k)?sh([[:space:]]+-[^[:space:]]*)*[[:space:]]+-[a-zA-Z]*c|eval)([[:space:]]|$)" <<<"$cmd"; then
  deny "commands run through sh -c or eval"
fi

# git: reading subcommands only; anything else is blocked. fetch is allowed
# without a refspec, so it updates remote-tracking refs only.
while IFS= read -r call; do
  [[ $call =~ ^git${gopts}[[:space:]]*$ ]] && continue # only options: --version, --help
  [[ $call =~ ^git${gopts}[[:space:]]+${word} ]] || deny "a git command the guard can't read"
  sub_rest
  [[ $rest =~ (^|[[:space:]])--output([[:space:]=]|$) ]] && deny "git --output (it writes a file)"
  [[ $sub == fetch && $rest =~ : ]] && deny "git fetch with a refspec (it can move a local branch)"
  case "$sub" in
    log | diff | show | status | blame | annotate | rev-parse | rev-list | ls-files | ls-tree | ls-remote | \
    cat-file | fetch | grep | describe | shortlog | merge-base | name-rev | for-each-ref | show-ref | \
    show-branch | whatchanged | range-diff | cherry | count-objects | check-ignore | check-attr | \
    check-mailmap | var | version | help | diff-tree | diff-index | diff-files | verify-commit | verify-tag) ;;
    reflog) [[ $rest =~ ^[[:space:]]*$|^[[:space:]]+(show|-) ]] || deny "git reflog (only show)" ;;
    notes) [[ $rest =~ ^[[:space:]]+(show|list)([[:space:]]|$) ]] || deny "git notes (only show and list)" ;;
    submodule) [[ $rest =~ ^[[:space:]]*$|^[[:space:]]+status([[:space:]]|$) ]] || deny "git submodule (only status)" ;;
    stash) [[ $rest =~ ^[[:space:]]+(list|show)([[:space:]]|$) ]] || deny "git stash (only list and show)" ;;
    worktree) [[ $rest =~ ^[[:space:]]+list([[:space:]]|$) ]] || deny "git worktree (only list)" ;;
    apply) [[ $rest =~ --(check|stat|numstat|summary) ]] || deny "git apply (only --check or --stat)" ;;
    config) [[ $rest =~ --(get|get-all|get-regexp|list|show-origin)|(^|[[:space:]])-l([[:space:]]|$) ]] || deny "git config (only reading)" ;;
    remote) [[ $rest =~ ^[[:space:]]*$|^[[:space:]]+(-v|--verbose|show|get-url)([[:space:]]|$) ]] || deny "git remote (only listing)" ;;
    tag)
      [[ $rest =~ ^[[:space:]]*$ || $rest =~ ^[[:space:]]+(-l|--list|-n[0-9]*|--contains|--no-contains|--points-at|--merged|--no-merged|--sort|--format)([[:space:]=]|$) ]] \
        || deny "git tag (only listing)" ;;
    branch)
      # git's rule: with a non-option word, branch creates one, unless list
      # mode is explicit (--list, or an option that takes a commit).
      if [[ $rest =~ (^|[[:space:]])(-[a-zA-Z]*[dDmMcCfu][a-zA-Z]*|--(delete|move|copy|force|set-upstream-to|unset-upstream|edit-description|track|no-track))([[:space:]=]|$) ]]; then
        deny "git branch changes"
      fi
      if [[ $rest =~ (^|[[:space:]])[^-[:space:]] ]] \
         && ! [[ $rest =~ (^|[[:space:]])(-l|--list|--contains|--no-contains|--merged|--no-merged|--points-at)([[:space:]=]|$) ]]; then
        deny "git branch with a name (it creates a branch; list with --list)"
      fi ;;
    *) deny "git $sub (not a reading subcommand)" ;;
  esac
done < <(calls git)

# gh: reading subcommands only, and `gh api` only as GET or a GraphQL query.
while IFS= read -r call; do
  call=$(sed -E 's/[[:space:]](-R|--repo)([[:space:]]+|=)[^[:space:]]+//g' <<<"$call")
  [[ $call =~ ^gh${gopts}[[:space:]]*$ ]] && continue # only options: --version, --help
  # The trailing space lets a verb at the very end match (bash's regex won't take $ in the group).
  [[ "$call " =~ ^gh[[:space:]]+([a-z][a-z-]*)([[:space:]]+([a-z][a-z-]*)[[:space:]])?(.*)$ ]] \
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
      if [[ $args =~ (^|[[:space:]])(-X|--method)([[:space:]]+|=)?([^[:space:]]+) && ${BASH_REMATCH[4]} != GET ]]; then
        deny "gh api with method ${BASH_REMATCH[4]} (only GET)"
      fi
      if grep -qE -- "(^|[[:space:]])(-f|-F|--field|--raw-field|--input)([[:space:]=]|$)" <<<"$args" \
         && ! grep -qE -- "(-X|--method)[[:space:]=]*GET" <<<"$args"; then
        deny "gh api with fields (a POST unless -X GET)"
      fi ;;
    *) deny "gh $sub $verb (only reading is allowed)" ;;
  esac
done < <(calls gh)

# docker: logs and state only.
while IFS= read -r call; do
  [[ $call =~ ^docker${dopts}[[:space:]]*$ ]] && continue # only options: --version, --help
  [[ $call =~ ^docker${dopts}[[:space:]]+${word} ]] || deny "a docker command the guard can't read"
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
if grep -qE "${at}(rm|rmdir|mv|cp|tee|truncate|chmod|chown|ln|mkdir|touch|dd|install|shred|unlink|rsync|npm|npx|pnpm|yarn|dotnet|node|python3?|ruby|php|unzip|tar|gunzip|gzip|bunzip2|unxz|xz|7z|curl|wget)([[:space:]]|$)" <<<"$cmd"; then
  deny "commands that change files, unpack, download, install or build (use jq to read JSON)"
fi
if grep -qE "${at}(sed|perl)[[:space:]]+([^;&|]*[[:space:]])?(-[a-zA-Z]*i|--in-place)" <<<"$cmd"; then
  deny "in-place edits"
fi
if grep -qE "${at}find[[:space:]][^;&|]*[[:space:]]-(delete|exec|execdir|ok|okdir|fprint[f0]?|fls)([[:space:]]|$)" <<<"$cmd"; then
  deny "find with -delete, -exec or a file output"
fi
# Redirection to a file; /dev/null and &N or &- (another descriptor) are fine.
while IFS= read -r redirect; do
  target=$(sed -E 's/^(&>>?|[0-9]*>>?\|?)[[:space:]]*//' <<<"$redirect")
  case "$target" in "" | /dev/null | "&"[0-9-]*) ;; *) deny "writing to $target" ;; esac
done < <(grep -oE '(&>>?|[0-9]*>>?\|?)[[:space:]]*(&[^[:space:]|;()<>]+|[^[:space:]|;&()<>]+)?' <<<"$cmd")

exit 0
