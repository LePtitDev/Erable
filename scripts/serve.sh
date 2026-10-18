#!/usr/bin/env bash
# Hosts an Erable server on this machine, without Docker (docs/deployment.md §1): publishes the server in Release
# (SPA included) into artifacts/server, then runs `erable serve`. The publication is redone when the sources changed
# (another commit) or with --rebuild. For Docker and HTTPS, see scripts/compose.sh.
# Usage: scripts/serve.sh [folder] [--port 5080] [--lan] [--rebuild] [-- other erable serve options]
#   folder     a project folder (shared as is) or a data folder (several projects); default: data/
#   --lan      listen on every interface (colleagues open http://<this machine>:<port>); default: this machine only
#   --rebuild  publish again even if the sources did not change
# First start: the server asks for the first administrator (or reads ERABLE_ADMIN_LOGIN / ERABLE_ADMIN_PASSWORD).
set -euo pipefail
cd "$(dirname "$0")/.."

export COREPACK_ENABLE_DOWNLOAD_PROMPT=0
export DOTNET_CLI_TELEMETRY_OPTOUT=1
# Corepack picks the pnpm version from the current folder's package.json: run it from src/web.
pnpm() { (cd src/web && corepack pnpm "$@"); }

folder="data"
port="5080"
lan=0
rebuild=0
extra=()
while [ $# -gt 0 ]; do
  case "$1" in
    --port) port="${2:?--port needs a value}"; shift 2 ;;
    --lan) lan=1; shift ;;
    --rebuild) rebuild=1; shift ;;
    --) shift; extra=("$@"); break ;;
    -*) echo "Unknown option '$1'. Use: [folder] --port N --lan --rebuild -- …" >&2; exit 2 ;;
    *) folder="$1"; shift ;;
  esac
done

output="artifacts/server"
stamp="$output/.source-commit"
commit="$(git rev-parse HEAD 2>/dev/null || echo unknown)"
if [ "$rebuild" = 1 ] || [ ! -f "$output/erable.dll" ] || [ "$(cat "$stamp" 2>/dev/null)" != "$commit" ]; then
  echo "==> Web dependencies and SPA"
  pnpm install --frozen-lockfile
  pnpm build
  echo "==> Server (Release) into $output"
  rm -rf "$output"
  dotnet publish src/Erable.Server/Erable.Server.csproj --configuration Release --output "$output" -p:UseAppHost=false
  echo "$commit" > "$stamp"
fi

mkdir -p "$folder"
args=(serve "$folder" --port "$port")
if [ "$lan" = 1 ]; then
  args+=(--lan)
  echo "Erable server on http://<this machine>:$port (every interface; plain HTTP, trusted network only)"
else
  echo "Erable server on http://127.0.0.1:$port"
fi
echo "Data: $folder · Ctrl+C stops the server"
# ${extra[@]+…}: an empty array is "unbound" for set -u in bash 3.2 (macOS).
exec dotnet "$output/erable.dll" "${args[@]}" ${extra[@]+"${extra[@]}"}
