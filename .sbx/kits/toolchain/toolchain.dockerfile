# The project's toolchain as a v3 tool mixin (#175): the .NET SDK, Node, and the
# two language servers the csharp-lsp and typescript-lsp plugins start. The
# claude kit (Debian 13, glibc 2.41) ships none of them, nor ICU, which .NET
# needs.
#
# Everything lands outside the agent's home and outside the claude kit's own
# directories, with links in /usr/local/bin, which is on the claude kit's PATH.
# Nothing depends on an ENV here: Docker's docs don't say a mixin's ENV reaches
# the sandbox. Each stage is pinned by digest; versions are in
# docs/architecture.md's table.

# The SDK from the same image and digest api/Dockerfile builds with.
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:fe0fd4e93bd2fbb47c883b7a6369e5c8d3db8bf01cdc5981f6ee35fbdf3ff4fb AS dotnet
ARG CSHARP_LS_VERSION=0.28.0
# --tool-path at its final location: the launcher finds its .store beside it.
RUN dotnet tool install --tool-path /usr/local/lib/dotnet-tools csharp-ls --version "${CSHARP_LS_VERSION}"
# ICU as this image runs .NET with it (Ubuntu 24.04, glibc 2.39, which a newer
# glibc runs). In the multiarch directory, which the loader searches by default.
RUN dir="$(dirname "$(find /usr/lib -name 'libicuuc.so.*' | head -n 1)")" \
 && mkdir -p "/icu${dir}" \
 && cp -a "${dir}"/libicu*.so.* "/icu${dir}/"

# Node, and the TypeScript language server with TypeScript 5.x: the 7.x native
# port ships no lib/tsserver.js, which the language server loads. In its own
# prefix, so the claude kit's npm-global directory keeps its ownership.
FROM node:24-trixie-slim@sha256:af02d57b57719b9657a45199a79264a9437940cd55b3c7b3fc99615654f124a0 AS node
ARG TS_LANGUAGE_SERVER_VERSION=6.1.0
ARG TYPESCRIPT_VERSION=5.9.3
RUN npm install --global --prefix /usr/local/lib/lsp \
      "typescript-language-server@${TS_LANGUAGE_SERVER_VERSION}" "typescript@${TYPESCRIPT_VERSION}"

FROM debian:trixie-slim@sha256:a72b4433de1cf7d6bae0c4cee74d0547f58601a643b3375f070c51600d63b6b2 AS assemble
COPY --from=dotnet /usr/share/dotnet /out/usr/share/dotnet
COPY --from=dotnet /usr/local/lib/dotnet-tools /out/usr/local/lib/dotnet-tools
COPY --from=dotnet /icu/ /out/
COPY --from=node /usr/local/bin/node /out/usr/local/bin/node
COPY --from=node /usr/local/lib/node_modules /out/usr/local/lib/node_modules
COPY --from=node /usr/local/lib/lsp /out/usr/local/lib/lsp
# The apphost of a .NET tool finds the runtime through this file, not PATH.
RUN mkdir -p /out/etc/dotnet \
 && echo /usr/share/dotnet > /out/etc/dotnet/install_location \
 && cd /out/usr/local/bin \
 && ln -s ../../share/dotnet/dotnet dotnet \
 && ln -s ../lib/dotnet-tools/csharp-ls csharp-ls \
 && ln -s ../lib/node_modules/npm/bin/npm-cli.js npm \
 && ln -s ../lib/node_modules/npm/bin/npx-cli.js npx \
 && ln -s ../lib/lsp/bin/typescript-language-server typescript-language-server \
 && ln -s ../lib/lsp/bin/tsserver tsserver

FROM scratch
COPY --from=assemble /out/ /
