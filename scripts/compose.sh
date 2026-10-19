#!/usr/bin/env bash
# Hosts Erable with Docker Compose behind Caddy, HTTPS included (docs/deployment.md §3 and §6). Wraps the commands of
# the documentation around deploy/docker-compose.yml and deploy/.env.
# Usage: scripts/compose.sh [--litestream] <command> [arguments]
#   setup [domain]       creates deploy/.env from the template, with a random password for the first administrator
#   up                   builds the image and starts (or updates) the server, then waits until it is healthy
#   update               same as up, also pulling newer base images
#   down                 stops the server (the data volume is kept)
#   status | logs [service]
#   backup [folder]      stops the server, archives the data volume into folder (default artifacts/backups), restarts
#   restore <archive>    stops the server and replaces the data volume by the archive (asks for confirmation)
#   doctor [project-id]  stops the server, checks every project (or one) with `erable doctor`, restarts
#   --litestream         adds the continuous backup (deploy/docker-compose.litestream.yml, LITESTREAM_* in deploy/.env)
set -euo pipefail
cd "$(dirname "$0")/.."

env_file="deploy/.env"
files=(-f deploy/docker-compose.yml)
if [ "${1:-}" = "--litestream" ]; then
  files+=(-f deploy/docker-compose.litestream.yml)
  shift
fi
command="${1:-}"
[ $# -gt 0 ] && shift

# Compose names the project after the folder of the first file (deploy) unless COMPOSE_PROJECT_NAME says otherwise.
project="${COMPOSE_PROJECT_NAME:-deploy}"
compose() { docker compose --env-file "$env_file" "${files[@]}" "$@"; }

# The version the image shows (Dockerfile): from Git like the other builds (build/version.mjs), or the tag of HEAD on
# a machine without Node; ERABLE_VERSION, from the environment or deploy/.env, wins.
if [ -z "${ERABLE_VERSION:-}" ] && ! grep -qs '^ERABLE_VERSION=.' "$env_file"; then
  ERABLE_VERSION="$(node build/version.mjs 2>/dev/null || git describe --tags --exact-match 2>/dev/null | sed 's/^v//' || true)"
  export ERABLE_VERSION
fi

require_env() {
  if [ ! -f "$env_file" ]; then
    echo "No $env_file: run scripts/compose.sh setup <domain> first." >&2
    exit 1
  fi
}

data_volume() {
  local volume
  volume="$(docker volume ls -q --filter "label=com.docker.compose.project=$project" --filter "label=com.docker.compose.volume=erable-data")"
  if [ -z "$volume" ]; then
    echo "No data volume for the compose project '$project': has the server been started (scripts/compose.sh up)?" >&2
    exit 1
  fi
  echo "$volume"
}

wait_healthy() {
  local id status
  id="$(compose ps -q erable)"
  for _ in $(seq 1 60); do
    status="$(docker inspect --format '{{.State.Health.Status}}' "$id" 2>/dev/null || echo starting)"
    if [ "$status" = healthy ]; then
      return 0
    fi
    sleep 2
  done
  echo "The server is not healthy after 2 minutes: scripts/compose.sh logs erable" >&2
  exit 1
}

# A throwaway Alpine container reads and writes the volume: the Erable image has no shell.
in_volume() { docker run --rm -v "$(data_volume):/data" "$@"; }

case "$command" in
  setup)
    if [ -f "$env_file" ]; then
      echo "$env_file already exists: edit it by hand." >&2
      exit 1
    fi
    # A finite read: `tr < /dev/urandom | head` dies of SIGPIPE under pipefail.
    password="$(head -c 96 /dev/urandom | base64 | LC_ALL=C tr -dc 'A-Za-z0-9' | cut -c 1-24)"
    sed -e "s|^ERABLE_ADMIN_PASSWORD=.*|ERABLE_ADMIN_PASSWORD=$password|" \
      -e "s|^ERABLE_DOMAIN=.*|ERABLE_DOMAIN=${1:-wiki.example.org}|" deploy/.env.example > "$env_file"
    chmod 600 "$env_file"
    echo "Created $env_file (readable by you only)."
    echo "First administrator: $(grep '^ERABLE_ADMIN_LOGIN=' "$env_file" | cut -d= -f2) / $password"
    [ -z "${1:-}" ] && echo "Set ERABLE_DOMAIN in $env_file: the domain must point to this machine, ports 80 and 443 open."
    echo "Then: scripts/compose.sh up"
    ;;
  up | update)
    require_env
    if [ "$command" = update ]; then
      compose build --pull
      compose pull caddy
    else
      compose build
    fi
    compose up -d
    echo "Waiting for the server…"
    wait_healthy
    echo "Erable is up: https://$(grep '^ERABLE_DOMAIN=' "$env_file" | cut -d= -f2)"
    ;;
  down)
    require_env
    compose down
    ;;
  status)
    require_env
    compose ps
    ;;
  logs)
    require_env
    compose logs --tail 200 -f "$@"
    ;;
  backup)
    require_env
    destination="${1:-artifacts/backups}"
    mkdir -p "$destination"
    destination="$(cd "$destination" && pwd)"
    archive="erable-data-$(date +%Y%m%d-%H%M%S).tar.gz"
    # Stopped first: a copy of SQLite files in use (WAL) may be inconsistent.
    compose stop erable
    trap 'compose start erable >/dev/null' EXIT
    in_volume -v "$destination:/backup" alpine tar czf "/backup/$archive" -C /data .
    echo "Backup: $destination/$archive"
    ;;
  restore)
    require_env
    archive="${1:?restore needs an archive made by scripts/compose.sh backup}"
    [ -f "$archive" ] || { echo "No file $archive" >&2; exit 1; }
    folder="$(cd "$(dirname "$archive")" && pwd)"
    name="$(basename "$archive")"
    read -r -p "Replace every project and account of the server by $name? Type 'yes': " answer
    [ "$answer" = yes ] || { echo "Cancelled."; exit 1; }
    compose stop erable
    trap 'compose start erable >/dev/null' EXIT
    # 1654: the user of the Erable image.
    in_volume -v "$folder:/backup:ro" alpine sh -c \
      "find /data -mindepth 1 -delete && tar xzf '/backup/$name' -C /data && chown -R 1654:1654 /data"
    echo "Restored from $name; check the projects with scripts/compose.sh doctor"
    ;;
  doctor)
    require_env
    if [ $# -gt 0 ]; then
      projects=("$1")
    else
      read -r -a projects <<< "$(in_volume alpine sh -c 'ls /data/projects 2>/dev/null' | tr '\n' ' ')"
    fi
    if [ ${#projects[@]} -eq 0 ]; then
      echo "No project on this server."
      exit 0
    fi
    # Closed projects only: doctor refuses a project the running server holds.
    compose stop erable
    trap 'compose start erable >/dev/null' EXIT
    status=0
    for id in "${projects[@]}"; do
      echo "==> $id"
      compose run --rm --no-deps --entrypoint dotnet erable erable.dll doctor "/data/projects/$id" || status=$?
    done
    exit $status
    ;;
  *)
    sed -n '4,13p' "$0" | sed 's/^# \{0,1\}//'
    [ -z "$command" ] && exit 0
    exit 2
    ;;
esac
