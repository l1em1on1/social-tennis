#!/usr/bin/env bash
# Tests for subagent-guard.sh: bash .claude/hooks/subagent-guard.test.sh
# Each command is passed to the guard as data, never run.
set -u
guard="$(dirname "$0")/subagent-guard.sh"
failures=0

check() {
  local want=$1 command=$2 got
  if jq -n --arg c "$command" '{tool_input:{command:$c}}' | bash "$guard" 2>/dev/null; then got=allow; else got=block; fi
  if [ "$got" != "$want" ]; then
    echo "FAIL: want $want, got $got: $command"
    failures=$((failures + 1))
  fi
}

while IFS= read -r line; do
  [ -z "$line" ] || [ "${line#\#}" != "$line" ] && continue
  check "${line%% *}" "${line#* }"
done <<'EOF'
# Reading
allow git diff origin/main...HEAD --stat
allow git log --oneline -5 | cat
allow git -C web show HEAD:package.json
allow git --no-pager log -1
allow git fetch origin main
allow git branch --show-current
allow git branch -a --contains HEAD
allow git stash list
allow git tag -l
allow git tag
allow git worktree list
allow git apply --check fix.patch
allow git config --get user.name
allow git remote -v
allow gh pr view 186 --json body
allow gh -R o/r pr view 5
allow gh --repo o/r run view 1 --log-failed | grep -E "Error|✘" | head -50
allow gh run list --limit 5
allow gh workflow view web.yml
allow gh release view v1
allow gh repo view
allow gh auth status
allow gh api repos/o/r/pulls/1/comments --jq ".[].body"
allow gh api -X GET search/code -f q='x repo:o/r'
allow gh api graphql -f query='query { repository(owner:"o", name:"r") { id } }'
allow docker compose --profile e2e logs web-e2e 2>&1 | tail -100
allow docker compose -p x ps
allow docker-compose logs api
allow docker logs pr-web-1 2>/dev/null
allow docker container ls
allow docker stats --no-stream
allow grep -rn "rating" web/src >&2
allow jq '.[] | select(.n > 1)' x.json
allow grep -n "git push" log.txt
allow grep -E '=> |->' out.log
allow sed -n 1,40p docs/architecture.md
allow ls -la .claude && find . -name "*.ts" | wc -l
allow cat web/playwright-report/index.html | head
allow timeout 60 gh run watch 1
allow env GH_PAGER=cat gh pr diff 1
allow rtk git status
allow rtk git log --oneline -3
allow rtk recall 245ad7ed464a5dee
allow rtk proxy git diff --stat
allow if git diff --quiet; then echo same; fi
allow for f in a.log b.log; do grep -c Error "$f"; done
allow git -P log -1
allow git -c core.pager=cat log -1
allow git reflog -5
allow git remote get-url origin
allow git submodule status
allow git notes show HEAD
allow grep -n "Can't resolve" ci.log | head
allow
# git writes
# from the CI review of #188
block rtk test docker compose run --rm api-tests
block rtk proxy git push
block rtk git push origin HEAD
block for f in web/test-results/*; do rm -rf "$f"; done
block if git push; then echo ok; fi
block ! git commit -m x
block { git push; }
block time git push
block grep -n "Can't resolve" ci.log; rm -rf web/test-results; echo 'done'
block git -P commit -am "x"
block git -P reset --hard
block git bisect start
block git format-patch -1
block git read-tree HEAD
block git checkout-index -a
block git sparse-checkout set web
block git reflog expire --all
block git notes add -m x
block git remote set-url origin x
block git frobnicate
block git push origin HEAD
block git commit -am "x"
block cd web && git checkout main
block git -C web reset --hard
block git branch -D old
block git branch -f main HEAD
block git branch newbranch
block git stash
block git stash pop
block git tag v1
block git worktree add ../x
block git apply fix.patch
block git config user.name x
block git remote add up url
block git update-index --assume-unchanged x
block git gc
block git notes add -m x
block git submodule update
# wrappers and shells
block env git push
block command git push
block timeout 10 git push
block nice git push
block sudo docker compose down
block /usr/bin/git push
block eval git push
block bash -c 'git push'
block sh -lc 'rm -rf x'
block echo x | xargs rm
# gh writes
block gh pr comment 186 --body hi
block gh pr merge 186
block gh issue edit 128 --body-file x
block gh -R o/r pr close 1
block gh api repos/o/r/issues/1/comments -f body=hi
block gh api -X DELETE repos/o/r/git/refs/heads/x
block gh api graphql -f query='mutation { addComment(input:{}) { clientMutationId } }'
block gh workflow run web.yml
block gh release create v1
# docker writes
block docker compose down -v
block docker compose --profile e2e run --rm e2e
block docker-compose down
block docker volume rm x
block docker container rm x
block docker run --rm alpine
block docker builder prune -af
# files, packages, builds
block rm -rf web/node_modules
block sed -i 's/a/b/' file.ts
block perl -pi -e 's/a/b/' file.ts
block find . -name x -delete
block find . -exec rm {} \;
block echo hi > notes.md
block echo "x" > f.md
block cat x >> CLAUDE.md
block ls | tee out.txt
block cmd &> out.txt
block cmd >| out.txt
block npm install
block npx playwright test
block dotnet build
block node -e 'require("fs").writeFileSync("x","y")'
block python3 -c 'open("x","w")'
EOF

if [ "$failures" -eq 0 ]; then echo "subagent-guard: all cases pass"; else exit 1; fi
