#!/usr/bin/env bash
# Runs Erable (build first with scripts/build.sh so that the SPA is up to date).
# Usage:
#   scripts/run.sh                         desktop application (default)
#   scripts/run.sh desktop
#   scripts/run.sh server [folder] [port]  erable serve, default folder artifacts/data, port 5080
#   scripts/run.sh dev                     Vite dev server on :5173 (proxy to a server started separately)
set -euo pipefail
cd "$(dirname "$0")/.."

export COREPACK_ENABLE_DOWNLOAD_PROMPT=0
export DOTNET_CLI_TELEMETRY_OPTOUT=1
mode="${1:-desktop}"
# Corepack picks the pnpm version from the current folder's package.json: run it from src/web.
pnpm() { (cd src/web && corepack pnpm "$@"); }

case "$mode" in
  desktop)
    dotnet run --project src/Erable.Desktop
    ;;
  server)
    folder="${2:-artifacts/data}"
    port="${3:-5080}"
    mkdir -p "$folder"
    echo "Erable server on http://127.0.0.1:$port (data: $folder)"
    dotnet run --project src/Erable.Server --no-launch-profile -- serve "$folder" --port "$port"
    ;;
  dev)
    pnpm dev
    ;;
  *)
    echo "Unknown mode '$mode'. Use: desktop | server [folder] [port] | dev" >&2
    exit 2
    ;;
esac
