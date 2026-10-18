@echo off
rem Hosts an Erable server on this machine, without Docker (docs\deployment.md section 1): publishes the server in
rem Release (SPA included) into artifacts\server, then runs `erable serve`. The publication is redone when the sources
rem changed (another commit) or with --rebuild. For Docker and HTTPS, see scripts\compose.bat.
rem Usage: scripts\serve.bat [folder] [--port 5080] [--lan] [--rebuild] [-- other erable serve options]
rem   folder     a project folder (shared as is) or a data folder (several projects); default: data\
rem   --lan      listen on every interface (colleagues open http://<this machine>:<port>); default: this machine only
rem   --rebuild  publish again even if the sources did not change
rem First start: the server asks for the first administrator (or reads ERABLE_ADMIN_LOGIN / ERABLE_ADMIN_PASSWORD).
setlocal EnableDelayedExpansion
cd /d "%~dp0.."

set "COREPACK_ENABLE_DOWNLOAD_PROMPT=0"
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
set "FOLDER=data"
set "PORT=5080"
set "LAN="
set "REBUILD=0"
set "EXTRA="

:args
if "%~1"=="" goto :publish
if "%~1"=="--" goto :extra
if /i "%~1"=="--port" (
  if "%~2"=="" (echo --port needs a value 1>&2 & exit /b 2)
  set "PORT=%~2" & shift & shift & goto :args
)
if /i "%~1"=="--lan" (set "LAN=--lan" & shift & goto :args)
if /i "%~1"=="--rebuild" (set "REBUILD=1" & shift & goto :args)
set "ARG=%~1"
if "!ARG:~0,1!"=="-" (
  echo Unknown option '%~1'. Use: [folder] --port N --lan --rebuild -- ... 1>&2
  exit /b 2
)
set "FOLDER=%~1" & shift & goto :args

:extra
shift
if "%~1"=="" goto :publish
set "EXTRA=!EXTRA! "%~1""
goto :extra

:publish
set "OUTPUT=artifacts\server"
set "STAMP=%OUTPUT%\.source-commit"
set "COMMIT=unknown"
for /f %%c in ('git rev-parse HEAD 2^>nul') do set "COMMIT=%%c"
set "PREVIOUS="
if exist "%STAMP%" set /p PREVIOUS=<"%STAMP%"
if "%REBUILD%"=="0" if exist "%OUTPUT%\erable.dll" if "!PREVIOUS!"=="%COMMIT%" goto :serve

echo ==^> Web dependencies and SPA
call :pnpm install --frozen-lockfile || exit /b 1
call :pnpm build || exit /b 1
echo ==^> Server (Release) into %OUTPUT%
if exist "%OUTPUT%" rmdir /s /q "%OUTPUT%"
dotnet publish src/Erable.Server/Erable.Server.csproj --configuration Release --output "%OUTPUT%" -p:UseAppHost=false || exit /b 1
> "%STAMP%" echo %COMMIT%

:serve
if not exist "%FOLDER%" mkdir "%FOLDER%"
if defined LAN (
  echo Erable server on http://^<this machine^>:%PORT% ^(every interface; plain HTTP, trusted network only^)
) else (
  echo Erable server on http://127.0.0.1:%PORT%
)
echo Data: %FOLDER% - Ctrl+C stops the server
dotnet "%OUTPUT%\erable.dll" serve "%FOLDER%" --port %PORT% %LAN% !EXTRA!
exit /b %ERRORLEVEL%

rem Corepack picks the pnpm version from the current folder's package.json: run it from src\web.
:pnpm
pushd src\web
call corepack pnpm %*
set "PNPM_EXIT=%ERRORLEVEL%"
popd
exit /b %PNPM_EXIT%
