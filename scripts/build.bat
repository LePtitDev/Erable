@echo off
rem Builds Erable: web dependencies, SPA (copied into the backend's wwwroot) and the .NET solution.
rem Usage: scripts\build.bat [Debug^|Release]
setlocal
cd /d "%~dp0.."

set "CONFIGURATION=%~1"
if "%CONFIGURATION%"=="" set "CONFIGURATION=Debug"
set "COREPACK_ENABLE_DOWNLOAD_PROMPT=0"
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"

echo ==^> Web dependencies
call :pnpm install --frozen-lockfile || goto :error

echo ==^> SPA
call :pnpm build || goto :error

echo ==^> .NET solution (%CONFIGURATION%)
dotnet build Erable.sln --configuration %CONFIGURATION% || goto :error

echo Build succeeded.
exit /b 0

:error
echo Build failed. 1>&2
exit /b 1

rem Corepack picks the pnpm version from the current folder's package.json: run it from src\web.
:pnpm
pushd src\web
call corepack pnpm %*
set "PNPM_EXIT=%ERRORLEVEL%"
popd
exit /b %PNPM_EXIT%
