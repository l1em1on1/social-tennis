#!/usr/bin/env bash
# Editor- and agent-facing toolchain: read-only code intelligence plus the
# Claude Code CLI. This is not a loosening of ADR-0005 — builds, tests,
# migrations and package installs still run as compose services, and none of
# this lands on the host, which needs nothing but Docker.
set -euo pipefail

sudo chown -R vscode:vscode /home/vscode/.claude

# C# language server for the csharp-lsp plugin.
dotnet tool install --global csharp-ls

# typescript-language-server 5.x loads the classic lib/tsserver.js, which the
# TypeScript 7 native port does not ship — pinning 5.x avoids a server that
# starts and then fails every request with "Could not find a valid TypeScript
# installation". 5.x also matches web/package.json, so LSP answers come from
# the same compiler version CI builds with.
npm install -g typescript-language-server typescript@5

npm install -g @anthropic-ai/claude-code

# Wire the language servers and Context7 into Claude Code. Enabling a plugin in
# .claude/settings.json is not enough on its own — the plugin still has to be
# installed, or its LSP/MCP tools never register.
claude plugin install csharp-lsp@claude-plugins-official -y
claude plugin install typescript-lsp@claude-plugins-official -y
claude plugin install context7@claude-plugins-official -y

dotnet --info
node --version
csharp-ls --version
typescript-language-server --version
