# Base images are pinned by tag and digest (#25): the tag says what it is, the
# digest makes the build reproducible and un-spoofable. Dependabot's docker
# updates move both together; the bump procedure is in docs/architecture.md
# ("Versions").
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:fe0fd4e93bd2fbb47c883b7a6369e5c8d3db8bf01cdc5981f6ee35fbdf3ff4fb AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/SocialTennis.Api -c Release -o /app

# Ubuntu chiseled: no shell, no package manager, and a non-root user (`app`,
# UID 1654) by default. The -extra variant adds ICU and tzdata, so strings
# compare and time zones resolve as on a full image (ADR-0016's Club zones).
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra@sha256:cae5b962bfb786606def03d250c6a645cc98839095c89e806e4bd8978a68115b
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
# The image's default already; stated so the runtime user is visible here.
USER $APP_UID
# No shell for a probe script, so the app probes itself (HealthProbe.cs).
HEALTHCHECK --interval=10s --timeout=5s --retries=3 --start-period=180s \
  CMD ["dotnet", "SocialTennis.Api.dll", "healthcheck"]
ENTRYPOINT ["dotnet", "SocialTennis.Api.dll"]
