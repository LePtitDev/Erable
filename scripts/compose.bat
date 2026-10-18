@echo off
rem Hosts Erable with Docker Compose behind Caddy, HTTPS included (docs\deployment.md sections 3 and 6). Wraps the
rem commands of the documentation around deploy\docker-compose.yml and deploy\.env.
rem Usage: scripts\compose.bat [--litestream] <command> [arguments]
rem   setup [domain]       creates deploy\.env from the template, with a random password for the first administrator
rem   up                   builds the image and starts (or updates) the server, then waits until it is healthy
rem   update               same as up, also pulling newer base images
rem   down                 stops the server (the data volume is kept)
rem   status | logs [service]
rem   backup [folder]      stops the server, archives the data volume into folder (default artifacts\backups), restarts
rem   restore <archive>    stops the server and replaces the data volume by the archive (asks for confirmation)
rem   doctor [project-id]  stops the server, checks every project (or one) with `erable doctor`, restarts
rem   --litestream         adds the continuous backup (deploy\docker-compose.litestream.yml, LITESTREAM_* in deploy\.env)
setlocal EnableDelayedExpansion
cd /d "%~dp0.."

set "ENV_FILE=deploy\.env"
set "FILES=-f deploy\docker-compose.yml"
if /i "%~1"=="--litestream" (
  set "FILES=!FILES! -f deploy\docker-compose.litestream.yml"
  shift
)
set "HELP=0"
set "COMMAND=%~1"
set "ARG=%~2"
rem Compose names the project after the folder of the first file (deploy) unless COMPOSE_PROJECT_NAME says otherwise.
set "PROJECT=%COMPOSE_PROJECT_NAME%"
if "%PROJECT%"=="" set "PROJECT=deploy"
set "COMPOSE=docker compose --env-file %ENV_FILE% %FILES%"

if /i "%COMMAND%"=="setup" goto :setup
if not exist "%ENV_FILE%" if not "%COMMAND%"=="" (
  echo No %ENV_FILE%: run scripts\compose.bat setup ^<domain^> first. 1>&2
  exit /b 1
)
if /i "%COMMAND%"=="up" goto :up
if /i "%COMMAND%"=="update" goto :up
if /i "%COMMAND%"=="down" (%COMPOSE% down & exit /b !ERRORLEVEL!)
if /i "%COMMAND%"=="status" (%COMPOSE% ps & exit /b !ERRORLEVEL!)
if /i "%COMMAND%"=="logs" (%COMPOSE% logs --tail 200 -f %ARG% & exit /b !ERRORLEVEL!)
if /i "%COMMAND%"=="backup" goto :backup
if /i "%COMMAND%"=="restore" goto :restore
if /i "%COMMAND%"=="doctor" goto :doctor
rem The usage is the header above (lines 4 to 13).
for /f "usebackq skip=3 tokens=* delims=" %%l in ("%~f0") do (
  set /a "HELP+=1"
  set "LINE=%%l"
  if !HELP! leq 10 echo(!LINE:~4!
)
if "%COMMAND%"=="" exit /b 0
exit /b 2

:setup
if exist "%ENV_FILE%" (
  echo %ENV_FILE% already exists: edit it by hand. 1>&2
  exit /b 1
)
set "DOMAIN=%ARG%"
if "%DOMAIN%"=="" set "DOMAIN=wiki.example.org"
for /f %%p in ('powershell -NoProfile -Command "-join ((48..57)+(65..90)+(97..122) | Get-Random -Count 24 | ForEach-Object {[char]$_})"') do set "PASSWORD=%%p"
powershell -NoProfile -Command "(Get-Content deploy\.env.example) -replace '^ERABLE_ADMIN_PASSWORD=.*', 'ERABLE_ADMIN_PASSWORD=%PASSWORD%' -replace '^ERABLE_DOMAIN=.*', 'ERABLE_DOMAIN=%DOMAIN%' | Set-Content -Encoding ascii %ENV_FILE%" || exit /b 1
for /f "tokens=2 delims==" %%a in ('findstr /b "ERABLE_ADMIN_LOGIN=" "%ENV_FILE%"') do set "LOGIN=%%a"
echo Created %ENV_FILE%.
echo First administrator: %LOGIN% / %PASSWORD%
if "%ARG%"=="" echo Set ERABLE_DOMAIN in %ENV_FILE%: the domain must point to this machine, ports 80 and 443 open.
echo Then: scripts\compose.bat up
exit /b 0

:up
if /i "%COMMAND%"=="update" (
  %COMPOSE% build --pull || exit /b 1
  %COMPOSE% pull caddy || exit /b 1
) else (
  %COMPOSE% build || exit /b 1
)
%COMPOSE% up -d || exit /b 1
echo Waiting for the server...
for /f %%i in ('%COMPOSE% ps -q erable') do set "ID=%%i"
for /l %%n in (1,1,60) do (
  for /f %%s in ('docker inspect --format "{{.State.Health.Status}}" !ID! 2^>nul') do set "STATUS=%%s"
  if "!STATUS!"=="healthy" goto :healthy
  timeout /t 2 /nobreak >nul
)
echo The server is not healthy after 2 minutes: scripts\compose.bat logs erable 1>&2
exit /b 1
:healthy
for /f "tokens=2 delims==" %%d in ('findstr /b "ERABLE_DOMAIN=" "%ENV_FILE%"') do set "DOMAIN=%%d"
echo Erable is up: https://%DOMAIN%
exit /b 0

:volume
set "VOLUME="
for /f %%v in ('docker volume ls -q --filter "label=com.docker.compose.project=%PROJECT%" --filter "label=com.docker.compose.volume=erable-data"') do set "VOLUME=%%v"
if "%VOLUME%"=="" (
  echo No data volume for the compose project '%PROJECT%': has the server been started ^(scripts\compose.bat up^)? 1>&2
  exit /b 1
)
exit /b 0

:backup
call :volume || exit /b 1
set "DEST=%ARG%"
if "%DEST%"=="" set "DEST=artifacts\backups"
if not exist "%DEST%" mkdir "%DEST%"
for %%f in ("%DEST%") do set "DEST=%%~ff"
for /f %%t in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd-HHmmss"') do set "ARCHIVE=erable-data-%%t.tar.gz"
rem Stopped first: a copy of SQLite files in use (WAL) may be inconsistent. The Erable image has no shell: Alpine copies.
%COMPOSE% stop erable || exit /b 1
docker run --rm -v "%VOLUME%:/data" -v "%DEST%:/backup" alpine tar czf "/backup/%ARCHIVE%" -C /data .
set "RESULT=%ERRORLEVEL%"
%COMPOSE% start erable >nul
if not "%RESULT%"=="0" exit /b %RESULT%
echo Backup: %DEST%\%ARCHIVE%
exit /b 0

:restore
if "%ARG%"=="" (
  echo restore needs an archive made by scripts\compose.bat backup 1>&2
  exit /b 2
)
if not exist "%ARG%" (
  echo No file %ARG% 1>&2
  exit /b 1
)
call :volume || exit /b 1
for %%f in ("%ARG%") do (set "FOLDER=%%~dpf" & set "NAME=%%~nxf")
set "FOLDER=%FOLDER:~0,-1%"
set /p ANSWER=Replace every project and account of the server by %NAME%? Type 'yes':
if not "%ANSWER%"=="yes" (
  echo Cancelled.
  exit /b 1
)
%COMPOSE% stop erable || exit /b 1
rem 1654: the user of the Erable image.
docker run --rm -v "%VOLUME%:/data" -v "%FOLDER%:/backup:ro" alpine sh -c "find /data -mindepth 1 -delete && tar xzf '/backup/%NAME%' -C /data && chown -R 1654:1654 /data"
set "RESULT=%ERRORLEVEL%"
%COMPOSE% start erable >nul
if not "%RESULT%"=="0" exit /b %RESULT%
echo Restored from %NAME%; check the projects with scripts\compose.bat doctor
exit /b 0

:doctor
call :volume || exit /b 1
set "PROJECTS=%ARG%"
if "%PROJECTS%"=="" (
  for /f %%p in ('docker run --rm -v "%VOLUME%:/data" alpine sh -c "ls /data/projects 2>/dev/null"') do set "PROJECTS=!PROJECTS! %%p"
)
if "%PROJECTS%"=="" (
  echo No project on this server.
  exit /b 0
)
rem Closed projects only: doctor refuses a project the running server holds.
%COMPOSE% stop erable || exit /b 1
set "RESULT=0"
for %%p in (%PROJECTS%) do (
  echo ==^> %%p
  %COMPOSE% run --rm --no-deps --entrypoint dotnet erable erable.dll doctor /data/projects/%%p
  if errorlevel 1 set "RESULT=!ERRORLEVEL!"
)
%COMPOSE% start erable >nul
exit /b %RESULT%
