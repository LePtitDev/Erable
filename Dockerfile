# syntax=docker/dockerfile:1
# Erable server image (docs/deployment.md). Build from the repository root:
#   docker build -t erable .
#   docker build -t erable --build-arg ERABLE_VERSION="$(node build/version.mjs)" .   (the version it shows)
# Run:   docker run -p 8080:8080 -v erable-data:/data -e ERABLE_ADMIN_LOGIN=admin -e ERABLE_ADMIN_PASSWORD=... erable

# ─── 1. The web client ───────────────────────────────────────────────────────
FROM node:22-alpine AS web
WORKDIR /src/web
RUN corepack enable
COPY src/web/package.json src/web/pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile
COPY src/web/ ./
# Vite alone: the type check and the lint belong to the CI, and the generated API client is committed.
RUN node node_modules/vite/bin/vite.js build

# ─── 2. The server ───────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/Erable.Core/Erable.Core.csproj src/Erable.Core/
COPY src/Erable.Storage/Erable.Storage.csproj src/Erable.Storage/
COPY src/Erable.Server/Erable.Server.csproj src/Erable.Server/
RUN dotnet restore src/Erable.Server/Erable.Server.csproj
COPY src/Erable.Core src/Erable.Core
COPY src/Erable.Storage src/Erable.Storage
COPY src/Erable.Server src/Erable.Server
# The server project serves the client from wwwroot/ when src/web/dist exists.
COPY --from=web /src/web/dist src/web/dist
# The version shown by the server (/api/info, home screen): the context has no .git to read it from, so it is given
# (release.yml, scripts/compose); 0.0.0-dev otherwise.
ARG ERABLE_VERSION=""
# Release: the /_test endpoints are not compiled in.
RUN dotnet publish src/Erable.Server/Erable.Server.csproj -c Release -o /app --no-restore -p:UseAppHost=false \
      "-p:Version=${ERABLE_VERSION:-0.0.0-dev}"
# The data folder, writable by the unprivileged user of the final image (a chiseled image has no shell to do it).
RUN mkdir /data && chown 1654:1654 /data

# ─── 3. The image ────────────────────────────────────────────────────────────
# Chiseled: no shell, no package manager, a non-root user (UID 1654).
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS final
WORKDIR /app
COPY --from=build /app ./
COPY --from=build --chown=1654:1654 /data /data
ENV ERABLE_ADDRESS=0.0.0.0 \
    ERABLE_PORT=8080 \
    DOTNET_EnableDiagnostics=0
USER 1654
VOLUME /data
EXPOSE 8080
# The image has no curl: the executable checks itself.
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 CMD ["dotnet", "erable.dll", "healthcheck"]
ENTRYPOINT ["dotnet", "erable.dll", "serve", "/data"]
