# Docker Sandboxes setup

Runs Claude Code inside a [Docker Sandbox](https://docs.docker.com/ai/sandboxes/)
(`sbx`), so an agent gets the project toolchain without anything landing on the
host (ADR-0005). VS Code connects to the same sandbox over SSH for editing: see
[VS Code](#vs-code).

- `Dockerfile` — sandbox template image: .NET SDK, the language servers behind
  the `csharp-lsp`/`typescript-lsp` plugins, the Context7 plugin, and RTK.
- `kits/context7/` — kit wiring the Context7 MCP server's API key through the
  sandbox proxy.
- `kits/github/` — kit giving the sandbox `gh` (issues, PRs) and `git push` to
  github.com over HTTPS, with the token held by the sandbox proxy.
- `kits/nuget/` — kit allowing egress to `api.nuget.org`, so `dotnet restore`
  works and `csharp-ls` can resolve packages. See [NuGet restore](#nuget-restore).
- `kits/rtk/` — kit registering RTK's Claude Code hook at sandbox creation; the
  `rtk` binary itself is baked into the template.

Kits use the `schemaVersion: "2"` field names — `permissions.network`,
`setup.install`, `agentInstructions.content`. The v1 names (`caps`, `commands`,
`agentContext`) are rejected by `sbx kit inspect` on current sbx; run it after
editing a kit, since `sbx kit validate` alone has let them through.

## Running

`sbx` does not share an image store with the Docker daemon — it has its own,
listed by `sbx template ls`. A locally built image is invisible to it, and
`sbx create -t <name>` falls back to pulling `docker.io/library/<name>`, which
fails with **403 Forbidden: pull failed for image**. Build, export, and load:

```bash
docker build -t social-tennis-sbx .sbx
docker save social-tennis-sbx:latest -o social-tennis-sbx.tar
sbx template load social-tennis-sbx.tar          # into the sandbox image store
```

Store the secrets — both prompt for the value (see
[GitHub access](#github-access) and [Context7 API key](#context7-api-key)):

```bash
sbx secret set -g github          # PAT of the l1em1on1-claude account
sbx secret set -g context7        # raw key, no `Bearer `
```

Then create the sandbox with the kits attached:

```bash
sbx create --name social-tennis -t social-tennis-sbx \
  --kit ./.sbx/kits/context7 --kit ./.sbx/kits/github \
  --kit ./.sbx/kits/nuget --kit ./.sbx/kits/rtk claude .
sbx settings set claude.remoteControl true       # see Remote Control below
sbx run --name social-tennis -- --remote-control
```

Kits are attached at creation only. To give an existing sandbox a new kit,
`sbx kit add social-tennis ./.sbx/kits/<kit>` recreates its container with the
kit appended, or `sbx rm social-tennis` and run `../sandbox.sh` again. The
workspace is a bind mount, so neither touches the repo.

Re-run the build/save/load trio after any change to `Dockerfile`; `sbx template
ls` shows what the sandbox runtime actually holds.

`../sandbox.sh` runs `claude update` inside the sandbox on every start, before
attaching, so the sandbox never lags behind the template's Claude Code version.
A failed update (e.g. offline) prints a warning and attaches anyway.

Each mixin kit's `agentInstructions` goes into a per-kit memory file that sbx
links from a `## Kits` section of its generated `CLAUDE.md`, outside the
workspace, so the repo's own tracked `CLAUDE.md` is never modified. Claude
Code reads both.

## Remote Control

[Remote Control](https://code.claude.com/docs/en/remote-control) drives the
sandbox's Claude Code session from claude.ai/code or the Claude mobile app.
`../sandbox.sh` sets it up on every run, in two parts:

- **`sbx settings set claude.remoteControl true`**, on the host. sbx keeps
  Remote Control's environment and session credentials on the host, and its
  proxy supplies them to a sandbox only while this setting is on
  ([sbx docs](https://docs.docker.com/ai/sandboxes/agents/claude-code/#remote-control)).
  With it off, `/remote-control` inside the sandbox registers a session that
  never connects. The setting is host-wide — it covers every sandbox on the
  machine — and takes effect on running sandboxes within seconds.
- **`sbx run ... -- --remote-control`**, so the session starts already
  connected instead of waiting for `/remote-control`. It appears in the session
  list under the sandbox's hostname, `social-tennis`.

There is no kit for it and no extra egress. Remote Control only makes outbound
requests to `api.anthropic.com`, which the `claude` agent is allowed already;
`claude.ai` is opened on the phone or browser, not from the sandbox.

**It needs a claude.ai sign-in inside the sandbox**: run `/login` once and
choose the subscription account. An API key stored with `sbx secret set
anthropic` runs Claude Code but cannot use Remote Control.

`../sandbox.sh --no-remote-control` attaches without it and leaves the sbx
setting as it is. To turn it off for running sandboxes too:

```bash
sbx settings set claude.remoteControl false
```

The sandbox runs Claude Code with `--dangerously-skip-permissions`, so whoever
is signed in to that claude.ai account can run anything the sandbox can —
which is the sandbox's own limits: the allow-listed egress and the
`l1em1on1-claude` GitHub token.

## VS Code

VS Code edits inside the sandbox over SSH
([sbx docs](https://docs.docker.com/ai/sandboxes/integrations/vscode/)): files,
terminals and extensions run in the sandbox, under its network policy. The
host needs VS Code with the Remote - SSH extension
(`ms-vscode-remote.remote-ssh`) and an OpenSSH client. macOS and most Linux
distributions include one; on Windows, install the OpenSSH client (an optional
Windows feature). PuTTY is not supported.

1. `./sandbox.sh --editor` creates the sandbox if needed, runs
   `sbx setup ssh`, prints the host and folder, and exits without starting
   Claude. `sbx setup ssh` writes a managed block to `~/.ssh/config`
   (`%USERPROFILE%\.ssh\config` on Windows); don't edit that block by hand.
   It is safe to re-run.
2. Check from a terminal: `ssh social-tennis.sbx`.
3. In VS Code, run **Remote-SSH: Connect to Host...** and type
   `social-tennis.sbx`. The host list doesn't show sandbox names. Then
   **Open Folder** at the path the script printed: the workspace keeps its
   host path, so `C:\Projects\AI\social-tennis` is
   `/c/Projects/AI/social-tennis` inside.
4. Install the recommended extensions when VS Code offers them
   (`.vscode/extensions.json`).

Connecting starts a stopped sandbox. SSH ends at the sbx daemon, which accepts
a connection only while you are signed in to Docker; no SSH server runs in the
sandbox.

**Network.** The first connection installs the VS Code Server in the sandbox.
If the sandbox can't download it, VS Code downloads it on the host and copies
it over. Extensions, and the C# extension's own dependencies, download from
inside the sandbox, partly from hosts the policy doesn't allow yet (such as
the extension CDN, `*.gallerycdn.vsassets.io`, and
`download.visualstudio.microsoft.com`). Each one comes up as a pending
approval on the host:

```bash
sbx policy approval ls
sbx policy approval respond <approval-id> --option <option-id>
```

Once the first connection has shown which hosts VS Code needs, they can move
into a kit.

**Ports.** `docker compose up` in a VS Code terminal publishes the web app on
port 3000 inside the sandbox. Forward it from VS Code's Ports view and open
the local address it shows.

**Not covered yet:** a debugger configuration (`.vscode/launch.json`).

## Context7 API key

CLAUDE.md requires querying Context7 before touching library or framework code,
so the sandbox needs the key.

**The Docker MCP Toolkit's copy cannot be reused.** Adding Context7 to the MCP
Toolkit stores the key at `docker/mcp/context7.api_key` in the OS keychain, but
that entry is write-only from our side: `docker mcp secret` offers only
`ls`/`rm`/`set`, and reading it through the credential helper returns
`credentials not found in native keychain`. The toolkit's key reaches the
toolkit's own gateway and nothing else. `sbx` needs its own copy.

The kit's `credentials:` entry takes its value from sbx's secret store, matched
by `service:` — here `context7`. It cannot read host environment variables or
files, so an exported `CONTEXT7_API_KEY` on the host does nothing
([kit spec reference](https://docs.docker.com/ai/sandboxes/customize/kit-reference/)).

`../sandbox.sh` asks for the key (from the [Context7 dashboard](https://context7.com/dashboard))
with a hidden prompt the first time and stores it with `sbx secret set -g
context7`, piped on stdin. Later runs reuse the stored secret without asking.
To rotate it, `../sandbox.sh --set-secret context7` asks for the new key,
replaces the stored one, and exits. Store the raw `ctx7sk-...` key — the kit's `format: Bearer
%s` adds the prefix.

`sbx secret import` will not help here: it only recognises its built-in services
(anthropic, github, openai, …), and `context7` is not one of them.

### How the key reaches Context7

The key stays on the host. `kits/context7/spec.yaml` declares it `proxyManaged`,
so inside the sandbox `CONTEXT7_API_KEY` holds a sentinel, not the key. The
Context7 plugin sends `Authorization: ${CONTEXT7_API_KEY}` to
`https://mcp.context7.com/mcp`, and the sandbox proxy rewrites that header to
`Bearer <real key>` on the way out. A shell inside the sandbox cannot read the
key, and neither can anything the agent runs.

`permissions.network.allow` lists `mcp.context7.com` because sandbox egress denies by
default — without it, tool calls fail at the proxy rather than at Context7.

### Why a kit credential rather than `sbx secret set-custom`

`set-custom` uses the same placeholder swap, but it is declared imperatively on
each machine, and the stored value must carry the `Bearer ` prefix itself. The
kit keeps the wiring — env var, target domain, header, format — in the repo, so
the only per-machine step is the key itself. Follow the same pattern for future
integrations: declare the credential in a kit, store the value under its
`service:` name.

For the record, the `set-custom` equivalent — not used here — needs no kit
`credentials:` block, but the value carries the prefix and every flag is
repeated by hand on each machine:

```bash
sbx secret set-custom -g \
  --host mcp.context7.com \
  --env CONTEXT7_API_KEY \
  --value "Bearer ctx7sk-..."
```

`--host` is the domain whose outbound requests get the placeholder swapped
(repeatable; wildcards like `*.example.com` allowed), and `--env` is the sandbox
variable set to the placeholder. The value is passed as an argument, so it
lands in shell history and the process list — `set-custom` has no stdin option.

Don't combine the two: both set `CONTEXT7_API_KEY` in the sandbox and would
fight over it. A custom secret left over from an earlier setup can be found
with `sbx secret ls -g` and removed with `sbx secret rm -g --placeholder <value>`.

Use sbx **v0.42.0 or later**. Earlier versions dropped the injected header for
kit credentials that used `scheme: bearer` without an explicit `header:`
([sbx-releases#490](https://github.com/docker/sbx-releases/issues/490)). This
kit names `header: Authorization`, so it should not hit that bug, but the fix
is where kit-declared services were confirmed working.
## GitHub access

The `github` kit lets the agent read and update issues with `gh`, and push
branches with `git`. It reuses sbx's built-in `github` service secret, which the
proxy injects into requests to `github.com` and `api.github.com`
([credentials](https://docs.docker.com/ai/sandboxes/security/credentials/)). The
token never enters the container.

The agent works as its own GitHub account, **`l1em1on1-claude`**, a
collaborator with write access to this repo and nothing else. Its token is
entered by hand exactly like the Context7 key: `../sandbox.sh` asks for it
(hidden) when none is stored, and pipes it to `sbx secret set -g github` on
stdin. A stored token is never asked for again; to replace it, run
`../sandbox.sh --set-secret github`.

Create the token signed in as `l1em1on1-claude`, at
[github.com/settings/tokens](https://github.com/settings/tokens):

- **Fine-grained**, if its Resource owner list offers `l1em1on1`: only
  `l1em1on1/social-tennis`; Contents, Issues, Pull requests and Workflows —
  read and write. Metadata — read is added automatically.
- **Classic** otherwise — fine-grained tokens reach only repos owned by the
  token's own account or an organisation, not ones it collaborates on: scopes
  `repo` and `workflow` (`workflow` is needed to push changes under
  `.github/workflows/`). The account can reach only this repo, so `repo`
  reaches only this repo.
- **Expiration:** set one. When it lapses, `gh` and `git push` start failing
  with 401; run `../sandbox.sh --set-secret github` and paste a new token.

A separate account rather than your own token: every push, PR, and comment
the agent makes is attributed to `l1em1on1-claude`, and it can reach only
what that account was invited to. Pair it with a ruleset on `main` that
requires a PR with one approval (Repository admin in the bypass list), so the
agent can open PRs but only you merge them — the kit's "never push to `main`"
line is an instruction to the agent, not an enforcement.

Commits are authored and committed as `l1em1on1-claude`
(`356328941+l1em1on1-claude@users.noreply.github.com`) through the kit's
`GIT_AUTHOR_*` / `GIT_COMMITTER_*` environment variables. `git config
--global` would not do it: the bind-mounted repo's `.git/config` sets
`user.name`/`user.email` for the host, and repo config beats global config,
while the variables beat both and change nothing the host reads.

Two gaps the kit closes, both found by probing a sandbox:

- **`gh` refuses to run without a token in its environment**, even though the
  proxy rewrites the `Authorization` header anyway. The kit sets
  `GH_TOKEN=proxy-managed`; any non-empty value works.
- **SSH to GitHub is blocked** — the policy denies `github.com:22` — and this
  repo's `origin` is `git@github.com:...`. The kit's install step adds
  `url.https://github.com/.insteadOf git@github.com:` to the agent's *global*
  git config, so pushes go over HTTPS. The repo's own `.git/config` is shared
  with the host through the bind mount and stays on SSH.

Commit identity comes from the repo's git config, so sandbox commits are
authored as you.

## NuGet restore

`csharp-ls` resolves NuGet packages and sibling projects from each project's
`obj/project.assets.json`. Without egress to nuget.org, `dotnet restore` fails
with `NU1301 ... 403 (Forbidden)` and the C# language server can still parse
files but flags every package type (`WebApplicationFactory`, `Fact`, `Assert`)
as missing. The `nuget` kit allows `api.nuget.org`, which serves both the
service index and the package downloads.

An existing sandbox predates the kit; add it with
`sbx kit add social-tennis ./.sbx/kits/nuget` (recreates the container), or
or approve the pending `api.nuget.org` request with `sbx policy approval respond <approval-id> --option allow` (list the ID with `sbx policy approval ls`).

**Restores for the language servers are automatic (#121).** A `SessionStart`
hook (`.claude/hooks/lsp-restore.sh`, registered in `.claude/settings.json`)
keeps them current:

- `dotnet` (synchronous, seconds once the package cache is warm): runs
  `dotnet restore api/SocialTennis.slnx` when a project's
  `obj/project.assets.json` is missing, older than its project file or
  `api/Directory.Build.props`, or written for another checkout path.
- `npm` (async, about two minutes on the bind mount): runs
  `npm ci --ignore-scripts --no-bin-links` in `web/` when `node_modules` is
  missing or older than `package-lock.json`. `--no-bin-links` because the
  bind mount from a Windows host refuses symlinks.

Both are no-ops where `dotnet`/`npm` isn't installed, and log to
`/tmp/social-tennis-lsp-restore.log`.

**Container builds don't touch it.** `project.assets.json` holds absolute
paths (the package folder and the project path), and `obj/` is on the bind
mount. Compose's .NET services set `SOCIAL_TENNIS_CONTAINER_BUILD=true`, which
`api/Directory.Build.props` turns into `obj/container/` and `bin/container/`,
so a test run in Docker no longer overwrites the language server's restore.

If C# lookups still report every package type missing (`Fact`, `Assert`,
`WebApplicationFactory`), csharp-ls loaded the solution before a first-ever
restore finished: run `/reload-plugins`, or start a new session.
