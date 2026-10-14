#!/usr/bin/env bash
# Builds Erable: web dependencies, SPA (copied into the backend's wwwroot) and the .NET solution.
# Usage: scripts/build.sh [Debug|Release]
set -euo pipefail
cd "$(dirname "$0")/.."

configuration="${1:-Debug}"
export COREPACK_ENABLE_DOWNLOAD_PROMPT=0
export DOTNET_CLI_TELEMETRY_OPTOUT=1
# Corepack picks the pnpm version from the current folder's package.json: run it from src/web.
pnpm() { (cd src/web && corepack pnpm "$@"); }

echo "==> Web dependencies"
pnpm install --frozen-lockfile

echo "==> SPA"
pnpm build

echo "==> .NET solution ($configuration)"
dotnet build Erable.sln --configuration "$configuration"

echo "Build succeeded."
