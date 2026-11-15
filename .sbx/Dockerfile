FROM docker/sandbox-templates:claude-code-docker

USER root

RUN apt-get update && apt-get install -y \
    dotnet-runtime-10.0 \
    dotnet-sdk-10.0

USER agent

# Same pins as .devcontainer/post-create.sh (#113); bump both together.
RUN dotnet tool install --global csharp-ls --version 0.28.0
# dotnet global tools (csharp-ls) land here. Set in the image, not a profile
# script: the LSP plugin spawns the server from Claude Code's own environment,
# which is not a login shell. Same fix as .devcontainer/devcontainer.json.
ENV PATH="${PATH}:/home/agent/.dotnet/tools"
RUN npm install -g typescript-language-server@6.1.0 typescript@5.9.3

# RTK binary, pinned exactly as in .devcontainer/post-create.sh: installer at
# the v1.2.0 commit, RTK_VERSION so it never resolves "latest". Installed at
# build time, so the sandbox needs no egress for it. The Claude Code hook is
# registered by the rtk kit at sandbox creation instead: sbx writes
# ~/.claude/settings.json then, so a hook baked in here would not survive.
ARG RTK_INSTALLER_SHA=e288a18c04452fd26192f59132e5324a8bb6924d
ARG RTK_VERSION=v1.2.0
RUN curl -fsSL "https://raw.githubusercontent.com/rtk-ai/rtk/${RTK_INSTALLER_SHA}/install.sh" | RTK_VERSION=${RTK_VERSION} sh
ENV PATH="${PATH}:/home/agent/.local/bin"

RUN claude plugin marketplace add anthropics/claude-plugins-official
RUN claude plugin install csharp-lsp@claude-plugins-official 
RUN claude plugin install typescript-lsp@claude-plugins-official 
RUN claude plugin install context7@claude-plugins-official 