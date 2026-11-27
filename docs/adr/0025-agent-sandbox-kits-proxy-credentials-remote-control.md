---
status: accepted
---

# The agents' sandbox: v3 kits, credentials held by the proxy, Remote Control from a server in the sandbox

Agents work in a Docker Sandbox (`sbx`), which carries the toolchain so the host doesn't (ADR-0005, amendments of 2026-11-16 and 2026-11-17). This records how that sandbox is put together and why. The commands are in `.sbx/README.md`, and the mechanism is in `docs/architecture.md`.

## Kits

- **The wiring lives in the repo.** Each integration is a kit: egress, credentials, install hooks and agent instructions are declared in `.sbx/kits/`. The only step left for each machine is storing the secret values.
- **Hooks that change Claude Code's configuration run after the claude kit's.** The claude kit's install hook writes `~/.claude/settings.json` from scratch, and hooks run a provider's before its requirers'. So the toolchain, context7 and rtk kits declare `requires: [claude]`; otherwise the claude kit could overwrite what they write.
- **Nothing relies on a kit image's `ENV`.** Docker's docs don't say it reaches the sandbox. Tools are linked into `/usr/local/bin`, already on the claude kit's `PATH`. .NET finds its runtime through `/etc/dotnet/install_location`. Environment variables are set with the spec's args with `env:`.
- **TypeScript 5.x for the language server.** The 7.x native port ships no `lib/tsserver.js`, which `typescript-language-server` loads.
- **Claude Code updates on every start.** Otherwise the sandbox would stay at the version the claude kit's image had when it was created. A failed update, offline say, only warns and attaches anyway.

## Credentials

Credentials never enter the container: each kit's `credential@1` names a service in sbx's secret store, and the sandbox proxy puts the value on the outbound request. Inside the sandbox the variable holds a sentinel.

- **Context7's key gets its own copy in sbx.** The Docker MCP Toolkit stores the key in the OS keychain (`docker/mcp/context7.api_key`), but nothing can read it back: `docker mcp secret` offers only `ls`, `rm` and `set`, and the credential helper answers `credentials not found in native keychain`. The toolkit's key reaches the toolkit's own gateway and nothing else. The kit's credential can't read host environment variables or files either.
- **A kit credential, not `sbx secret set-custom`.** The kit keeps the wiring (the variable, the target domain, the header and its format) in the repo. `set-custom` does the same placeholder swap, but it is declared by hand on each machine, the stored value must carry the `Bearer ` prefix, and the value is passed as an argument (there's no stdin option), so it lands in shell history and the process list. `sbx secret import` knows only its built-in services (anthropic, github, openai, …), and `context7` isn't one. Future integrations follow the same pattern: declare the credential in a kit and store the value under its `service:` name.
- **GitHub: a separate account, `l1em1on1-claude`.** Every push, PR and comment the agent makes is attributed to that account, and it reaches only the repository it was invited to. The owner reviews and merges: GitHub doesn't let an author approve their own PR, and `main`'s ruleset requires an approval, which is what enforces it. The kit's "never push to `main`" is only an instruction. So the owner's own token never goes in the sandbox: with it, every PR the agent opened was the owner's, and the owner couldn't approve it (2026-11-18, #186 to #193; #197).
- **A classic token, with `repo` and `workflow`.** A fine-grained token reaches only repositories owned by its account or an organisation, not ones its account collaborates on. The account can reach only this repository, so `repo` reaches only it.
- **`Bearer` on `github.com` too.** Docker's docs say sbx doesn't use the spec's Basic `scheme` in v3 kits.
- **`GH_TOKEN` holds a sentinel.** `gh` refuses to run without a token in its environment, even though the proxy rewrites the header.
- **The commit identity comes from environment variables** (`GIT_AUTHOR_*`, `GIT_COMMITTER_*`). The bind-mounted repository's `.git/config` holds the host's identity, and repository config beats `--global`. The variables beat both and change nothing the host reads. The spec's `git-identity@1` supplies the host user's identity, and repository config would still win over it.
- **Pushes go over HTTPS.** The policy blocks SSH (`github.com:22`), and `origin` is an SSH address. The kit adds a `url.https://github.com/.insteadOf git@github.com:` rewrite to the agent's global git config, because the repository's own config is shared with the host, which stays on SSH.

## Remote Control

Remote Control (#179) lets the owner drive the sandbox's Claude Code from claude.ai/code or the phone, also after logging out of the host.

- **A host-wide sbx setting, not a kit.** sbx keeps Remote Control's credentials on the host, and its proxy supplies them to sandboxes only while `claude.remoteControl` is on. Remote Control makes outbound requests only to `api.anthropic.com`, which the claude agent may reach already, so it needs no extra egress. `claude.ai` is opened on the phone or in a browser, not from the sandbox.
- **The sandbox runs detached.** A sandbox started by `sbx create` "stops automatically when no sessions keep it running"; a detached one "keeps running until you stop it with `sbx stop` or remove it with `sbx rm`" ([usage](https://docs.docker.com/ai/sandboxes/usage/)).
- **The server is started with `sbx exec`, in tmux.** Its sessions then have the sandbox's whole environment: the commit identity and the sentinels. A kit startup hook sees only the variables it lists.
- **The script answers the server's first question itself.** On its first start the server asks "Enable Remote Control? (y/n)", and it isn't listed until that is answered. Nobody would see the question in a detached pane or at boot. Running `sandbox.sh` with Remote Control on is the consent. It types `y` only when the pane's last line is exactly that question, and only in the first 2 minutes of each start.
- **It needs a claude.ai sign-in.** An API key, or a `claude setup-token` token, runs Claude Code but can't use Remote Control.

## Starting at boot

`sandbox.sh --install-service` (#180) writes a systemd user unit. It is a oneshot that runs the script at boot, and lingering lets it run with no login.

- **`KillMode=process`.** With `RemainAfterExit=yes`, the sbx daemon the script starts stays in the unit's cgroup. That daemon runs every sandbox on the host. Under systemd's default, `control-group`, stopping the unit would kill the daemon and every other sandbox. With `process`, a stop runs `sbx stop <name>` and kills at most the script.
- **Not a `systemd-run --scope` wrapper.** At shutdown its scope can stop before `ExecStop` runs.

## Language servers

- **Packages are restored inside the sandbox.** `obj/project.assets.json` records absolute package and project paths, so a restore made anywhere else doesn't resolve.
- **Container builds use their own `obj/`.** `obj/` is on the bind mount, shared with the language server. Compose's .NET services write to `obj/container/` and `bin/container/`, so a build or test run never overwrites the language server's restore.
- **`npm ci --no-bin-links`.** The bind mount from a Windows host refuses symlinks.
- **The worktree hooks read their JSON with bash, not `jq`.** A Windows host's Git Bash has no `jq`.

## Plugins

The plugins are Anthropic's official ones that `.claude/settings.json` enables (2026-11-27, #271 and its follow-up).

- **A kit hook installs each one.** An entry in project settings turns a plugin on but downloads nothing, and the session starts without it ([plugin docs](https://code.claude.com/docs/en/plugins/loading.md#enabled-in-project-settings-but-not-installed)). The toolchain kit's hook already adds the marketplace, so it installs them, all but `context7`, which its own kit installs. The list is kept by hand: a plugin added to `enabledPlugins` needs its line in the hook too.
- **Debian's Chromium for the Playwright MCP server.** The claude kit has no browser. Playwright's own download host needs an approval on each machine, while Debian's apt sources are already the image's. The plugin fixes the server's arguments, so the kit configures it through `PLAYWRIGHT_MCP_*` args with `env:`: Chromium at `/usr/bin/chromium`, headless (no display) and with an in-memory profile.
- **npm and PyPI egress, declared.** The server is fetched from npm on each start, and `security-guidance` installs the Agent SDK from PyPI on its first session. Both reached them before the kit allowed them, but a policy the kit doesn't declare may not hold on another machine.
- **RTK leaves `git commit` and `git push` alone.** `security-guidance` reviews a commit or push only when the command matches `Bash(git*commit*)` or `Bash(git*push*)`. RTK rewrites `git commit` to `rtk git commit`, which doesn't match, so with RTK as it was no commit was reviewed (tested 2026-11-27). Their output is short, so RTK saves little on them.
- **`security-guidance` spends the subscription's usage.** At each turn end it reviews the turn's diff on the newest Opus, and after each commit or push it runs an agentic review. That cost $0.01 to $0.08 at API prices on a small diff. `ENABLE_STOP_REVIEW=0` or `ENABLE_COMMIT_REVIEW=0` in the environment turns either off.

## Consequences

- Kits attach only when a sandbox is created, so changing a kit means removing the sandbox and creating it again. The workspace is a bind mount, so the repository is untouched.
- Each machine stores the GitHub token and the Context7 key once. A lapsed token makes `gh` and `git push` fail with 401 until it is replaced.
- Whoever is signed in to the claude.ai account can run anything the sandbox can, within its allow-listed egress and the `l1em1on1-claude` token.
