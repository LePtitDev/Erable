@echo off
rem Runs Erable (build first with scripts\build.bat so that the SPA is up to date).
rem Usage:
rem   scripts\run.bat                         desktop application (default)
rem   scripts\run.bat desktop
rem   scripts\run.bat server [folder] [port]  erable serve, default folder artifacts\data, port 5080
rem   scripts\run.bat dev                     Vite dev server on :5173 (proxy to a server started separately)
setlocal
cd /d "%~dp0.."

set "COREPACK_ENABLE_DOWNLOAD_PROMPT=0"
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
set "MODE=%~1"
if "%MODE%"=="" set "MODE=desktop"

if /i "%MODE%"=="desktop" (
  dotnet run --project src/Erable.Desktop
  exit /b %ERRORLEVEL%
)

if /i "%MODE%"=="server" (
  set "FOLDER=%~2"
  set "PORT=%~3"
  goto :server
)

if /i "%MODE%"=="dev" (
  call :pnpm dev
  exit /b %ERRORLEVEL%
)

echo Unknown mode '%MODE%'. Use: desktop ^| server [folder] [port] ^| dev 1>&2
exit /b 2

:server
if "%FOLDER%"=="" set "FOLDER=artifacts\data"
if "%PORT%"=="" set "PORT=5080"
if not exist "%FOLDER%" mkdir "%FOLDER%"
echo Erable server on http://127.0.0.1:%PORT% (data: %FOLDER%)
dotnet run --project src/Erable.Server --no-launch-profile -- serve "%FOLDER%" --port %PORT%
exit /b %ERRORLEVEL%

rem Corepack picks the pnpm version from the current folder's package.json: run it from src\web.
:pnpm
pushd src\web
call corepack pnpm %*
set "PNPM_EXIT=%ERRORLEVEL%"
popd
exit /b %PNPM_EXIT%
