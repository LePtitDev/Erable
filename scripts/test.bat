@echo off
rem Runs the checks of the PR pipeline locally (docs/testing.md, section 13).
rem Usage: scripts\test.bat [--e2e] [--desktop]
rem   (default)  .NET build and tests (Debug + Release), web typecheck, lint, format, unit tests, API contract
rem   --e2e      also Playwright Chromium + WebKit against the real backend (installs the browsers if needed)
rem   --desktop  also publishes the desktop app (win-x64), runs E2E-16-01/02 over CDP and its --self-test
setlocal
cd /d "%~dp0.."

set "COREPACK_ENABLE_DOWNLOAD_PROMPT=0"
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
set "E2E=0"
set "DESKTOP=0"

:args
if "%~1"=="" goto :run
if /i "%~1"=="--e2e" (set "E2E=1" & shift & goto :args)
if /i "%~1"=="--desktop" (set "DESKTOP=1" & shift & goto :args)
echo Unknown option '%~1'. Use: --e2e --desktop 1>&2
exit /b 2

:run
echo ==^> .NET (Debug)
dotnet format Erable.sln --verify-no-changes || goto :error
dotnet test --solution Erable.sln || goto :error
echo ==^> .NET (Release: /_test endpoints must be absent)
dotnet test --solution Erable.sln --configuration Release || goto :error

echo ==^> Web
call :pnpm install --frozen-lockfile || goto :error
call :pnpm typecheck || goto :error
call :pnpm lint || goto :error
call :pnpm format:check || goto :error
call :pnpm test || goto :error
call :pnpm api:check || goto :error
call :pnpm build || goto :error
call :pnpm bundle-size || goto :error
call :pnpm e2e:trace --enforce M0 || goto :error

if "%E2E%"=="1" (
  echo ==^> E2E ^(Chromium + WebKit^)
  call :pnpm exec playwright install chromium webkit || goto :error
  call :pnpm e2e || goto :error
)

if "%DESKTOP%"=="1" (
  echo ==^> Desktop ^(win-x64^): publish, CDP smoke and self-test
  node build/publish-desktop.mjs win-x64 --skip-web || goto :error
  call :pnpm e2e:desktop || goto :error
  if exist artifacts\desktop\self-test.json del artifacts\desktop\self-test.json
  rem From a batch file, cmd waits for GUI programs: the exit code is the self-test result.
  artifacts\desktop\win-x64\Erable\Erable.Desktop.exe --self-test --self-test-report artifacts\desktop\self-test.json || goto :error
  type artifacts\desktop\self-test.json
)

echo All checks passed.
exit /b 0

:error
echo Checks failed. 1>&2
exit /b 1

rem Corepack picks the pnpm version from the current folder's package.json: run it from src\web.
:pnpm
pushd src\web
call corepack pnpm %*
set "PNPM_EXIT=%ERRORLEVEL%"
popd
exit /b %PNPM_EXIT%
