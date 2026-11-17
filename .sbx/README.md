# Docker Sandboxes setup

Runs Claude Code inside a [Docker Sandbox](https://docs.docker.com/ai/sandboxes/)
(`sbx`), so an agent gets the project toolchain without anything landing on the
host (ADR-0005). It is for agents: people edit on the host and run the stack
with `docker compose` (see the main README).

The sandbox is sbx's built-in `claude` agent with five **v3 mixin kits**
(#175):

- `kits/toolchain/`: the .NET 10 SDK (with ICU), Node 24, `csharp-ls` and
  `typescript-language-server`, linked into `/usr/local/bin`, plus an install
  hook that adds Claude Code's `csharp-lsp` and `typescript-lsp` plugins.
- `kits/context7/`: the Context7 MCP server's API key, through the sandbox
  proxy, and the Context7 plugin.
- `kits/github/`: `gh` (issues, PRs) and `git push` to github.com over HTTPS as
  `l1em1on1-claude`, with the token held by the sandbox proxy.
- `kits/nuget/`: egress to `api.nuget.org`, so `dotnet restore` works and
  `csharp-ls` can resolve packages. See [NuGet restore](#nuget-restore).
- `kits/rtk/`: the RTK binary, and its Claude Code hook, registered at creation.

Each kit is a directory with a descriptor, `<kit>.yaml`. The toolchain and rtk
kits also have `<kit>.dockerfile`, their image content. The format is Docker's
[sandbox kit spec](https://github.com/docker/sandbox-kit-spec), v3. Its
capabilities include `network-policy@1` (egress), `credential@1` (a
proxy-held secret), `lifecycle@1` (install hooks) and `agent-context@1`/`@2`
(instructions for the agent).

**Why v3.** sbx 0.51 made the built-in `claude` agent a published v3 kit
(`docker.io/sbx/claude`), and a sandbox can't mix v3 kits with v1 or v2 ones.
The v2 setup, a custom template image plus four v2 kits, could no longer
create a sandbox. The template is gone: its tools are now the toolchain kit's
image content, which sbx builds itself.

**Checking a kit after editing it.** Build it with the kit frontend. It decodes
the descriptor strictly, so an unknown field is an error, and it needs only
Docker:

```bash
docker buildx build -f .sbx/kits/<kit>/<kit>.yaml -t sbx-kit-<kit>:dev .sbx/kits/<kit>
```

On the host, `sbx kit inspect ./.sbx/kits/<kit>` also checks the kit against
what that sbx supports.

**Hooks run after the claude kit's.** The claude kit's own install hook writes
`~/.claude/settings.json` from scratch. Install hooks run in dependency order,
a provider's before its requirers'. So the kits that change Claude Code's
configuration (toolchain, context7 and rtk, for plugins and RTK's hook)
declare `requires: [claude]`. Otherwise what they write could be overwritten.

**Nothing relies on a kit's `ENV`.** Docker's docs don't say a mixin's image
`ENV` reaches the sandbox. The tools are linked into `/usr/local/bin`, which is
already on the claude kit's `PATH`. .NET finds its runtime through
`/etc/dotnet/install_location`. The github kit's commit identity uses args
with `env:`, the spec's way to set the container's environment.

## Running

`../sandbox.sh` does all of this. It needs sbx 0.51 or later, and checks the
version. By hand:

Store the secrets. Both prompt for the value (see
[GitHub access](#github-access) and [Context7 API key](#context7-api-key)).
They are global, for every sandbox: that is sbx's default since v0.38.0, and
the old `-g` flag now only prints a deprecation warning (#173).

```bash
sbx secret set github             # PAT of the l1em1on1-claude account
sbx secret set context7           # raw key, no `Bearer `
```

Then create the sandbox with the kits. The first time, sbx builds the
toolchain and rtk kits' images, which takes a few minutes. It builds a kit
directory on demand, cached by its source, with the host's Docker Buildx
([kit spec README](https://github.com/docker/sandbox-kit-spec#readme),
"The local loop"). That needs a builder that exports OCI images, which
Buildx's default `docker` driver can't do with Docker's classic image store:
`OCI exporter is not supported for the docker driver`. So `../sandbox.sh`
creates a `docker-container` builder, `sbx-kit-builder`, once, and selects it
with `BUILDX_BUILDER` for `sbx create`. By hand:

```bash
docker buildx create --name sbx-kit-builder --driver docker-container --bootstrap
BUILDX_BUILDER=sbx-kit-builder sbx create ...     # as below
```

The builder is a BuildKit container on the host's Docker, and it keeps the
build cache. `docker buildx rm sbx-kit-builder` removes both. Setting
`SBX_KIT_BUILDER=sandbox` instead builds in a builder sandbox, with no host
Docker needed; the script leaves either variable alone when it is already
set.

```bash
sbx create --name social-tennis \
  --kit ./.sbx/kits/toolchain --kit ./.sbx/kits/context7 --kit ./.sbx/kits/github \
  --kit ./.sbx/kits/nuget --kit ./.sbx/kits/rtk claude .
sbx settings set claude.remoteControl true       # see Remote Control below
sbx run --name social-tennis -- --remote-control
```

Kits are attached at creation only. After changing a kit, run
`sbx rm social-tennis` and then `../sandbox.sh` again. The workspace is a bind
mount, so neither touches the repo. To try a changed kit beside a sandbox that
works, `../sandbox.sh --name <another-name>` creates a second sandbox. Both
work on the same checkout, so don't work in both at once.

`../sandbox.sh` runs `claude update` inside the sandbox on every start, before
attaching. Otherwise the sandbox would stay at the Claude Code version the
claude kit's image had when the sandbox was created. A failed update (e.g.
offline) prints a warning and attaches anyway.

Each kit's `agent-context` text reaches Claude Code through
`~/.claude/CLAUDE.md` in the sandbox. sbx writes it into a section it manages
there and keeps the rest of the file. That file is outside the workspace, so
the repo's own tracked `CLAUDE.md` is never modified. Claude Code reads both.

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

## Context7 API key

CLAUDE.md requires querying Context7 before touching library or framework code,
so the sandbox needs the key.

**The Docker MCP Toolkit's copy cannot be reused.** Adding Context7 to the MCP
Toolkit stores the key at `docker/mcp/context7.api_key` in the OS keychain, but
that entry is write-only from our side: `docker mcp secret` offers only
`ls`/`rm`/`set`, and reading it through the credential helper returns
`credentials not found in native keychain`. The toolkit's key reaches the
toolkit's own gateway and nothing else. `sbx` needs its own copy.

The kit's `credential@1` entry takes its value from sbx's secret store,
matched by `service:`. Here that is `context7`. It cannot read host environment variables or
files, so an exported `CONTEXT7_API_KEY` on the host does nothing
([credential@1](https://github.com/docker/sandbox-kit-spec/blob/main/docs/spec/capabilities/com.docker.sandbox/credential@1.md)).

`../sandbox.sh` asks for the key (from the [Context7 dashboard](https://context7.com/dashboard))
with a hidden prompt the first time and stores it with `sbx secret set
context7`, piped on stdin. Later runs reuse the stored secret without asking.
To rotate it, `../sandbox.sh --set-secret context7` asks for the new key,
replaces the stored one, and exits. Store the raw `ctx7sk-...` key — the kit's `format: Bearer
%s` adds the prefix.

`sbx secret import` will not help here: it only recognises its built-in services
(anthropic, github, openai, …), and `context7` is not one of them.

### How the key reaches Context7

The key stays on the host. `kits/context7/context7.yaml` declares it `proxyManaged`,
so inside the sandbox `CONTEXT7_API_KEY` holds a sentinel, not the key. The
Context7 plugin sends `Authorization: ${CONTEXT7_API_KEY}` to
`https://mcp.context7.com/mcp`, and the sandbox proxy rewrites that header to
`Bearer <real key>` on the way out. A shell inside the sandbox cannot read the
key, and neither can anything the agent runs.

The kit's `network-policy@1` allows `mcp.context7.com` at runtime, because
sandbox egress denies by default. Without it, tool calls would fail at the
proxy rather than at Context7. It also allows `github.com` while install hooks
run, because the plugin's marketplace is a git repository.

### Why a kit credential rather than `sbx secret set-custom`

`set-custom` uses the same placeholder swap, but it is declared imperatively on
each machine, and the stored value must carry the `Bearer ` prefix itself. The
kit keeps the wiring (env var, target domain, header, format) in the repo, so
the only per-machine step is the key itself. Follow the same pattern for future
integrations: declare the credential in a kit, store the value under its
`service:` name.

For the record, the `set-custom` equivalent — not used here — needs no kit
credential, but the value carries the prefix and every flag is
repeated by hand on each machine:

```bash
sbx secret set-custom \
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
with `sbx secret ls` and removed with `sbx secret rm --placeholder <value>`.

## GitHub access

The `github` kit lets the agent read and update issues with `gh`, and push
branches with `git`. Its `credential@1` entry uses sbx's built-in `github`
service secret, which the proxy sets as `Authorization: Bearer <token>` on
requests to `github.com` and `api.github.com`
([credentials](https://docs.docker.com/ai/sandboxes/configuration/credentials/)).
The token never enters the container. Bearer on `github.com` too, for `git`:
Docker's docs say sbx doesn't use the spec's Basic `scheme` in v3 kits.

The agent works as its own GitHub account, **`l1em1on1-claude`**, a
collaborator with write access to this repo and nothing else. Its token is
entered by hand exactly like the Context7 key: `../sandbox.sh` asks for it
(hidden) when none is stored, and pipes it to `sbx secret set github` on
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
`GIT_AUTHOR_*` / `GIT_COMMITTER_*` environment variables, set by four args
with `env:`. `git config
--global` would not do it: the bind-mounted repo's `.git/config` sets
`user.name`/`user.email` for the host, and repo config beats global config,
while the variables beat both and change nothing the host reads. The spec's
`git-identity@1` capability doesn't fit: it supplies the host user's identity,
and repository config still wins over it.

Two gaps the kit closes, both found by probing a sandbox:

- **`gh` refuses to run without a token in its environment**, even though the
  proxy rewrites the `Authorization` header anyway. The credential names
  `GH_TOKEN`, so sbx sets it to a sentinel, which is enough.
- **SSH to GitHub is blocked** — the policy denies `github.com:22` — and this
  repo's `origin` is `git@github.com:...`. The kit's install step adds
  `url.https://github.com/.insteadOf git@github.com:` to the agent's *global*
  git config, so pushes go over HTTPS. The repo's own `.git/config` is shared
  with the host through the bind mount and stays on SSH.

## NuGet restore

`csharp-ls` resolves NuGet packages and sibling projects from each project's
`obj/project.assets.json`. Without egress to nuget.org, `dotnet restore` fails
with `NU1301 ... 403 (Forbidden)` and the C# language server can still parse
files but flags every package type (`WebApplicationFactory`, `Fact`, `Assert`)
as missing. The `nuget` kit allows `api.nuget.org`, which serves both the
service index and the package downloads.

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
