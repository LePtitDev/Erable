@echo off
rem Builds the Windows installer, as the release workflow does (docs\release.md): self-contained publication, then
rem Velopack. The files land in artifacts\release\win-x64\ (Erable-win-Setup.exe, portable archive, packages);
rem erable-cli (docs\cli.md) in artifacts\cli\.
rem Usage: scripts\publish.bat [--version x.y.z] [--android]
rem   --version  default from Git: the tag of HEAD, or x.y.(z+1)-dev.N after the last tag
rem   --android  the Android APK only, in artifactsndroid\ (needs the android workload; signing: docselease.md)
rem Unsigned unless ERABLE_SIGN_AZURE_METADATA is set (docs\release.md).
setlocal
cd /d "%~dp0.."

set "COREPACK_ENABLE_DOWNLOAD_PROMPT=0"
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
set "VERSION_ARGS="
set "ANDROID=0"

:args
if "%~1"=="" goto :run
if "%ANDROID%"=="1" (
  echo ==^> Android application
  node build/publish-android.mjs %VERSION_ARGS% || goto :error
  exit /b 0
)
if /i "%~1"=="--version" (
  if "%~2"=="" (echo --version needs a value 1>&2 & exit /b 2)
  set "VERSION_ARGS=--version %~2" & shift & shift & goto :args
)
if /i "%~1"=="--android" (set "ANDROID=1" & shift & goto :args)
echo Unknown option '%~1'. Use: --version x.y.z --android 1>&2
exit /b 2

:run
echo ==^> Web dependencies
call :pnpm install --frozen-lockfile || goto :error

echo ==^> Desktop installer (win-x64)
node build/pack-desktop.mjs win-x64 %VERSION_ARGS% || goto :error

echo ==^> Command-line tool (win-x64)
node build/publish-cli.mjs win-x64 %VERSION_ARGS% || goto :error

echo Installer and packages in artifacts\release\win-x64\:
dir /b artifacts\release\win-x64
echo Command-line tool: artifacts\cli\erable-cli-win-x64.zip
exit /b 0

:error
echo Publication failed. 1>&2
exit /b 1

rem Corepack picks the pnpm version from the current folder's package.json: run it from src\web.
:pnpm
pushd src\web
call corepack pnpm %*
set "PNPM_EXIT=%ERRORLEVEL%"
popd
exit /b %PNPM_EXIT%
