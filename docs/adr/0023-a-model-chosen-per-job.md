# A model chosen per job: subagents for reading, workflows for sweeps

The agent's sessions, its subagents and workflows, and the labelled CI review all draw on the owner's one Claude subscription, and Opus uses the most per task. Subagents start with an empty context, so delegating small work costs more than doing it. Delegation pays when the input is large and the answer small, or when a fresh context is the point.

So the main session (Opus) keeps everything that needs the conversation's context or acts: planning, code, review answers, commits, pushes and GitHub posts. Work goes to a cheaper or fresher context only where that pays (#172, owner's answers 2026-11-17):

- **Reading at volume, on Haiku, without asking:** the built-in `Explore` agent for file sweeps, and `log-reader` for long CI, Playwright and container logs.
- **A fresh review, on Opus at high effort, without asking:** `pre-pr-reviewer` checks every code PR's branch before it opens. Text-only PRs skip it.
- **Sweeps, as dynamic workflows, asking before each run:** many similar edits (one Storybook story per primitive, #46) or an audit across many files (#106), with cheap models per item and Opus checking the result.
- **The CI review:** Opus at high effort on every labelled PR (#171). The model isn't chosen by path, and the review uses no subagents.

Only the main session changes git or GitHub. The two read-only agents get `Bash, Read, Grep, Glob`, and a `PreToolUse` hook (`.claude/hooks/subagent-guard.sh`; its tests run in CI as `docker compose run --rm guard-tests`) allows only git's reading subcommands, reading `gh` calls and Docker's logs and state, and blocks file changes, installs and builds, and anything run through `sh -c` or `eval`. It looks through shell keywords and wrappers (`sudo`, `timeout`, `rtk`, …), and fails closed when it can't read a command. It matches command text, so it guards against mistakes, not against intent.

## Consequences

- A delegation beyond the table in `CLAUDE.md` needs the owner's OK first.
- Every subagent finding is checked against the code before it's acted on.
- Workflows must be enabled in `/config` (Dynamic workflows), per user.
- After the first three tickets that delegate, #128 records what was delegated and whether it saved context or cost more.

## Considered

- **Everything in the main session.** Simplest, but long logs and wide sweeps fill its context.
- **Sonnet for docs-only CI reviews.** Not now: labelling a PR is already the cost control, and one model is simpler.
- **Subagents inside the CI review.** No: they multiply usage per review, and `Agent` would join the review's tool allow-list.
