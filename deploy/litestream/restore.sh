#!/usr/bin/env bash
# Restores the databases of an Erable server from their Litestream replica (docs/deployment.md §6).
#   deploy/litestream/restore.sh <volume> <project-id>...
# <volume>: the Docker volume of /data (created empty, or emptied of the damaged databases);
# <project-id>: the folders under projects/ of the bucket. The server must be stopped.
# The same LITESTREAM_* variables as the compose file must be set in the environment.
set -euo pipefail

if [ "$#" -lt 1 ]; then
  echo "Usage: $0 <volume> [<project-id>...]" >&2
  exit 2
fi
volume=$1
shift
image=${LITESTREAM_IMAGE:-litestream/litestream:0.3.13}
config=$(cd "$(dirname "$0")" && pwd)/litestream.yml

restore() {
  docker run --rm --user 0:0 \
    -v "$volume:/data" -v "$config:/etc/litestream.yml:ro" \
    -e LITESTREAM_BUCKET -e LITESTREAM_ENDPOINT -e LITESTREAM_REGION \
    -e LITESTREAM_ACCESS_KEY_ID -e LITESTREAM_SECRET_ACCESS_KEY -e LITESTREAM_FORCE_PATH_STYLE \
    "$image" restore -config /etc/litestream.yml -if-db-not-exists -if-replica-exists "$1"
}

restore /data/server.db
for id in "$@"; do
  restore "/data/projects/$id/erable.db"
done

# The manifests and the attachments, copied by the files-backup service of the compose overlay.
docker run --rm --user 0:0 -v "$volume:/data"   -e RCLONE_CONFIG_BACKUP_TYPE=s3 -e RCLONE_CONFIG_BACKUP_PROVIDER=Other   -e RCLONE_CONFIG_BACKUP_ENDPOINT="$LITESTREAM_ENDPOINT" -e RCLONE_CONFIG_BACKUP_REGION="${LITESTREAM_REGION:-us-east-1}"   -e RCLONE_CONFIG_BACKUP_ACCESS_KEY_ID="$LITESTREAM_ACCESS_KEY_ID" -e RCLONE_CONFIG_BACKUP_SECRET_ACCESS_KEY="$LITESTREAM_SECRET_ACCESS_KEY"   -e RCLONE_CONFIG_BACKUP_FORCE_PATH_STYLE="${LITESTREAM_FORCE_PATH_STYLE:-false}"   "${RCLONE_IMAGE:-rclone/rclone:1.68.2}" copy "backup:$LITESTREAM_BUCKET/files" /data/projects

# The restored files belong to root: give them back to the user of the server image (UID 1654).
docker run --rm --user 0:0 -v "$volume:/data" --entrypoint chown "$image" -R 1654:1654 /data
echo "Restored: databases, manifests and attachments. Start the server, then run 'erable doctor' on each project."
