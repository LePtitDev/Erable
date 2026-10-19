#!/usr/bin/env bash
# Builds the desktop installer of this machine's system, as the release workflow does (docs/release.md): self-contained
# publication, then Velopack. The files land in artifacts/release/<rid>/ (Setup, AppImage or pkg, portable archive);
# erable-cli (docs/cli.md) in artifacts/cli/.
# Usage: scripts/publish.sh [--version x.y.z] [--rid win-x64|linux-x64|osx-arm64|osx-x64] [--android]
#   --version  default from Git: the tag of HEAD, or x.y.(z+1)-dev.N after the last tag
#   --rid      default: this machine (macOS can also pack the other architecture)
#   --android  the Android APK only, in artifacts/android/ (needs the android workload; signing: docs/release.md)
# Unsigned unless the ERABLE_SIGN_* variables of docs/release.md are set.
set -euo pipefail
cd "$(dirname "$0")/.."

export COREPACK_ENABLE_DOWNLOAD_PROMPT=0
export DOTNET_CLI_TELEMETRY_OPTOUT=1
# Corepack picks the pnpm version from the current folder's package.json: run it from src/web.
pnpm() { (cd src/web && corepack pnpm "$@"); }

version=()
android=0
case "$(uname -s)-$(uname -m)" in
  Linux-x86_64) rid=linux-x64 ;;
  Darwin-arm64) rid=osx-arm64 ;;
  Darwin-x86_64) rid=osx-x64 ;;
  MINGW*-x86_64 | MSYS*-x86_64 | CYGWIN*-x86_64) rid=win-x64 ;;
  *) rid="" ;;
esac
while [ $# -gt 0 ]; do
  case "$1" in
    --version) version=(--version "${2:?--version needs a value}"); shift 2 ;;
    --rid) rid="${2:?--rid needs a value}"; shift 2 ;;
    --android) android=1; shift ;;
    *) echo "Unknown option '$1'. Use: --version x.y.z --rid <rid> --android" >&2; exit 2 ;;
  esac
done
if [ "$android" = 1 ]; then
  echo "==> Android application"
  node build/publish-android.mjs ${version[@]+"${version[@]}"}
  exit 0
fi
if [ -z "$rid" ]; then
  echo "Unsupported platform: $(uname -s)-$(uname -m). Give --rid." >&2
  exit 2
fi

echo "==> Web dependencies"
pnpm install --frozen-lockfile

echo "==> Desktop installer ($rid)"
# ${version[@]+…}: an empty array is "unbound" for set -u in bash 3.2 (macOS).
node build/pack-desktop.mjs "$rid" ${version[@]+"${version[@]}"}

echo "==> Command-line tool ($rid)"
node build/publish-cli.mjs "$rid" ${version[@]+"${version[@]}"}

echo "Installer and packages in artifacts/release/$rid/:"
ls -1 "artifacts/release/$rid"
echo "Command-line tool: artifacts/cli/erable-cli-$rid.*"
