# Docker Sandboxes setup

Runs Claude Code inside a [Docker Sandbox](https://docs.docker.com/ai/sandboxes/)
(`sbx`), so an agent gets the project toolchain without anything landing on the
host — the same constraint ADR-0005 places on the Dev Container.

- `Dockerfile` — sandbox template image: .NET SDK, the language servers behind
  the `csharp-lsp`/`typescript-lsp` plugins, and the Context7 plugin.
- `kits/context7/` — kit wiring the Context7 MCP server's API key through the
  sandbox proxy.

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

Store the Context7 key under the kit's service name (raw key, no `Bearer `;
see [Context7 API key](#context7-api-key)):

```bash
sbx secret set -g context7        # prompts for the value
```

Then create the sandbox with the kit attached:

```bash
sbx create --name social-tennis -t social-tennis-sbx --kit ./.sbx/kits/context7 claude .
sbx run --name social-tennis
```

Re-run the build/save/load trio after any change to `Dockerfile`; `sbx template
ls` shows what the sandbox runtime actually holds.

`../sandbox.sh` runs `claude update` inside the sandbox on every start, before
attaching, so the sandbox never lags behind the template's Claude Code version.
A failed update (e.g. offline) prints a warning and attaches anyway.

The kit's `agentContext` is written to a generated `CLAUDE.md` one directory
*above* the workspace inside the sandbox, so the repo's own tracked `CLAUDE.md`
is never modified. Claude Code reads both.

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
`../sandbox.sh --rebuild` asks again so the key can be rotated; press Enter to
keep the stored one. Store the raw `ctx7sk-...` key — the kit's `format: Bearer
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

`caps.network.allow` lists `mcp.context7.com` because sandbox egress denies by
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