# syntax=docker/dockerfile:1

# --- Build stage -------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, using only project files, to maximize Docker layer caching.
COPY src/ArtifactBrowser/ArtifactBrowser.csproj src/ArtifactBrowser/
COPY src/ArtifactBrowser.Client/ArtifactBrowser.Client.csproj src/ArtifactBrowser.Client/
RUN dotnet restore src/ArtifactBrowser/ArtifactBrowser.csproj

COPY src/ src/
# Do not pass --no-restore: in .NET 10, blazor.web.js comes from a restore-time
# pack (Microsoft.AspNetCore.App.Internal.Assets). A csproj-only restore before
# source is copied can miss it, and publish --no-restore then ships no script.
RUN dotnet publish src/ArtifactBrowser/ArtifactBrowser.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# --- Runtime stage -------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
ARG APP_UID=1654

# curl is required for the container HEALTHCHECK below; nothing else is added.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0 \
    TZ=Etc/UTC \
    ArtifactBrowser__ContentRoot=/data \
    ArtifactBrowser__CacheRoot=/cache

# /data is expected to be bind-mounted read-only; /cache and /app/logs must be writable.
RUN mkdir -p /data /cache /app/logs \
    && chown -R "${APP_UID}:${APP_UID}" /cache /app/logs

COPY --from=build /app/publish .
RUN chown -R "${APP_UID}:${APP_UID}" /app

USER ${APP_UID}
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl --fail --silent --max-time 3 http://127.0.0.1:8080/health || exit 1

ENTRYPOINT ["dotnet", "ArtifactBrowser.dll"]
