# Docker Sandboxes setup

Runs Claude Code inside a [Docker Sandbox](https://docs.docker.com/ai/sandboxes/)
(`sbx`), so an agent gets the project toolchain without anything landing on the
host — the same constraint ADR-0005 places on the Dev Container.

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
sbx run --name social-tennis
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

**`obj/` is shared.** `project.assets.json` holds absolute paths — the package
folder and the project path. The workspace bind mount means the sandbox
(`/c/Users/.../social-tennis`, packages in `/home/agent/.nuget`) and the Dev
Container (`/workspaces/social-tennis`, packages in `/home/vscode/.nuget`)
overwrite each other's restore output. Whichever restored last has working C#
LSP; the other needs `dotnet restore api/SocialTennis.slnx` again. Even a
*failed* restore rewrites the file, with an empty package list.
