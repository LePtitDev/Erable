#!/usr/bin/env bash
# End-to-end test of the Litestream backup (docs/deployment.md §6): a server with a local S3 (MinIO) creates a
# project and a page, the databases and files are replicated, the data volume is destroyed, restored from the bucket,
# and the page is found again. Needs Docker; takes a few minutes (it builds the server image).
#   deploy/litestream/test-restore.sh
set -euo pipefail

cd "$(dirname "$0")/../.."
project=erable-restore-test
compose=(docker compose -p "$project"
  -f docker-compose.yml -f docker-compose.litestream.yml -f litestream/test/minio.compose.yml)
network="${project}_default"
volume="${project}_erable-data"

export ERABLE_ADMIN_LOGIN=admin ERABLE_ADMIN_PASSWORD=restore-test-2026 ERABLE_DOMAIN=localhost
export LITESTREAM_BUCKET=erable-backup LITESTREAM_ENDPOINT=http://minio:9000 LITESTREAM_REGION=us-east-1
export LITESTREAM_ACCESS_KEY_ID=test-access-key LITESTREAM_SECRET_ACCESS_KEY=test-secret-key-0123456789
export LITESTREAM_FORCE_PATH_STYLE=true FILES_BACKUP_INTERVAL_SECONDS=5

cleanup() { "${compose[@]}" down --volumes --remove-orphans >/dev/null 2>&1 || true; }
trap cleanup EXIT

# The API from inside the compose network (the server is not published, and its image has no curl).
api() { docker run --rm --network "$network" curlimages/curl:8.11.1 --silent --show-error --fail-with-body "$@"; }
json() { printf '%s' "$1" | sed -n "s/.*\"$2\":\"\([^\"]*\)\".*/\1/p" | head -n 1; }

echo "== Start the server, Litestream and MinIO"
"${compose[@]}" up -d --build erable litestream files-backup minio bucket
until [ "$(docker inspect --format '{{.State.Health.Status}}' "${project}-erable-1")" = healthy ]; do sleep 2; done

echo "== Create a project and a page"
session=$(api -X POST http://erable:8080/api/v1/auth/login -H 'Content-Type: application/json' \
  -d '{"login":"admin","password":"restore-test-2026","issueToken":true}')
token=$(json "$session" token)
auth=(-H "Authorization: Bearer $token" -H 'Content-Type: application/json')
created=$(api -X POST http://erable:8080/api/v1/projects "${auth[@]}" -d '{"name":"Sauvegarde","contentLanguage":"fr"}')
project_id=$(json "$created" id)
page=$(api -X POST "http://erable:8080/api/v1/projects/$project_id/pages" "${auth[@]}" \
  -d '{"parentId":"0000000000000001","name":"Page à retrouver","markdown":"Contenu sauvegardé."}')
page_id=$(json "$page" id)
echo "project $project_id, page $page_id"

echo "== Wait for the replication"
sleep 20
"${compose[@]}" logs litestream | tail -n 5

echo "== Destroy the data volume"
"${compose[@]}" stop erable litestream files-backup
docker volume rm -f "$volume" >/dev/null
docker volume create "$volume" >/dev/null

echo "== Restore from the bucket"
litestream_restore() {
  docker run --rm --network "$network" --user 0:0 -v "$volume:/data" -v "$PWD/litestream/litestream.yml:/etc/litestream.yml:ro"     -e LITESTREAM_BUCKET -e LITESTREAM_ENDPOINT -e LITESTREAM_REGION -e LITESTREAM_ACCESS_KEY_ID     -e LITESTREAM_SECRET_ACCESS_KEY -e LITESTREAM_FORCE_PATH_STYLE litestream/litestream:0.3.13     restore -config /etc/litestream.yml -if-replica-exists "$1"
}
litestream_restore /data/server.db
litestream_restore "/data/projects/$project_id/erable.db"
docker run --rm --network "$network" -v "$volume:/data" --user 0:0   -e RCLONE_CONFIG_BACKUP_TYPE=s3 -e RCLONE_CONFIG_BACKUP_PROVIDER=Other -e RCLONE_CONFIG_BACKUP_ENDPOINT=http://minio:9000   -e RCLONE_CONFIG_BACKUP_ACCESS_KEY_ID=test-access-key -e RCLONE_CONFIG_BACKUP_SECRET_ACCESS_KEY=test-secret-key-0123456789   -e RCLONE_CONFIG_BACKUP_FORCE_PATH_STYLE=true rclone/rclone:1.68.2 copy backup:erable-backup/files /data/projects
docker run --rm -v "$volume:/data" --user 0:0 --entrypoint chown litestream/litestream:0.3.13 -R 1654:1654 /data

echo "== The restored project is sound (erable doctor)"
"${compose[@]}" run --rm --no-deps --entrypoint dotnet erable erable.dll doctor "/data/projects/$project_id"

echo "== Start again: the accounts and the page are back"
"${compose[@]}" up -d erable
until [ "$(docker inspect --format '{{.State.Health.Status}}' "${project}-erable-1")" = healthy ]; do sleep 2; done
session=$(api -X POST http://erable:8080/api/v1/auth/login -H 'Content-Type: application/json' \
  -d '{"login":"admin","password":"restore-test-2026","issueToken":true}')
token=$(json "$session" token)
content=$(api "http://erable:8080/api/v1/projects/$project_id/pages/$page_id/content" -H "Authorization: Bearer $token")
case "$content" in
  *"Contenu sauvegardé."*) echo "OK: the page was restored from the backup." ;;
  *) echo "FAILED: $content" >&2; exit 1 ;;
esac
