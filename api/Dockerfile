# Base images are pinned by tag and digest (#25): the tag says what it is, the
# digest makes the build reproducible and un-spoofable. Dependabot's docker
# updates move both together; the bump procedure is in docs/architecture.md
# ("Versions").
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:8c6edfb26c85153f236d26efebd9ad349aec403583f343f201ee67838b23658d AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/SocialTennis.Api -c Release -o /app

# Ubuntu chiseled: no shell, no package manager, and a non-root user (`app`,
# UID 1654) by default. The -extra variant adds ICU and tzdata, so strings
# compare and time zones resolve as on a full image (ADR-0016's Club zones).
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra@sha256:c8dd08a50d8ffd19bd8da72edee05a7ac15b486a5b1869cf46fdb370bf1fbc76
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
