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

Then create the sandbox with the kit attached:

```bash
sbx create --name social-tennis -t social-tennis-sbx --kit ./.sbx/kits/context7 claude .
sbx run --name social-tennis
```

Re-run the build/save/load trio after any change to `Dockerfile`; `sbx template
ls` shows what the sandbox runtime actually holds.

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

Set `CONTEXT7_API_KEY` (from the [Context7 dashboard](https://context7.com/dashboard))
in the environment you run `sbx create` from — persistently, so it survives new
shells:

```powershell
setx CONTEXT7_API_KEY "ctx7sk-..."   # new terminals only; restart the shell
```

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

### Alternative: keep the key out of the host environment

`sbx secret set-custom` stores the value in sbx's own keychain instead of an env
var. It is the same placeholder-swap mechanism, but declared imperatively per
machine rather than in the committed kit, and the stored value has to include
the `Bearer ` prefix because nothing applies a format string to it:

```bash
sbx secret set-custom -g --host mcp.context7.com --env CONTEXT7_API_KEY --value "Bearer ctx7sk-..."
```

Use one or the other. Both set `CONTEXT7_API_KEY` in the sandbox, so running
both leaves the kit and the custom secret fighting over the same variable —
drop the `credentials:` block from the kit if you go this way.
