#!/usr/bin/env bash
# Runs the checks of the PR pipeline locally (docs/testing.md §13).
# Usage: scripts/test.sh [--e2e] [--desktop]
#   (default)  .NET build and tests (Debug + Release), web typecheck, lint, format, unit tests, API contract
#   --e2e      also Playwright Chromium + WebKit against the real backend (installs the browsers if needed)
#   --desktop  also publishes the desktop app for this machine and runs its --self-test
set -euo pipefail
cd "$(dirname "$0")/.."

export COREPACK_ENABLE_DOWNLOAD_PROMPT=0
export DOTNET_CLI_TELEMETRY_OPTOUT=1
e2e=false
desktop=false
for arg in "$@"; do
  case "$arg" in
    --e2e) e2e=true ;;
    --desktop) desktop=true ;;
    *) echo "Unknown option '$arg'. Use: --e2e --desktop" >&2; exit 2 ;;
  esac
done
# Corepack picks the pnpm version from the current folder's package.json: run it from src/web.
pnpm() { (cd src/web && corepack pnpm "$@"); }

echo "==> .NET (Debug)"
dotnet format Erable.sln --verify-no-changes
dotnet test --solution Erable.sln
echo "==> .NET (Release: /_test endpoints must be absent)"
dotnet test --solution Erable.sln --configuration Release

echo "==> Web"
pnpm install --frozen-lockfile
pnpm typecheck
pnpm lint
pnpm format:check
pnpm test
pnpm api:check
pnpm build
pnpm bundle-size
pnpm e2e:trace --enforce M0

if $e2e; then
  echo "==> E2E (Chromium + WebKit)"
  pnpm exec playwright install chromium webkit
  pnpm e2e
fi

if $desktop; then
  case "$(uname -s)-$(uname -m)" in
    Linux-x86_64) rid=linux-x64 ;;
    Darwin-arm64) rid=osx-arm64 ;;
    Darwin-x86_64) rid=osx-x64 ;;
    *) echo "Unsupported platform for --desktop: $(uname -s)-$(uname -m)" >&2; exit 2 ;;
  esac
  echo "==> Desktop ($rid): publish and self-test"
  node build/publish-desktop.mjs "$rid" --skip-web
  "artifacts/desktop/$rid/Erable/Erable.Desktop" --self-test
fi

echo "All checks passed."
