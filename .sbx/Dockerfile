FROM docker/sandbox-templates:claude-code-docker

USER root

RUN apt-get update && apt-get install -y \
    dotnet-runtime-10.0 \
    dotnet-sdk-10.0

USER agent

RUN dotnet tool install --global csharp-ls
RUN npm install -g typescript-language-server typescript@5

RUN claude plugin marketplace add anthropics/claude-plugins-official
RUN claude plugin install csharp-lsp@claude-plugins-official 
RUN claude plugin install typescript-lsp@claude-plugins-official 
RUN claude plugin install context7@claude-plugins-official 