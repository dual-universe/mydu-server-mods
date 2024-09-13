#!/bin/bash

# Configuration
MAX_BACKUPS=7  # Maximum number of backups to keep
BACKUP_DIR="backup"
SERVER_DIR="/DU_Server"

log() {
    echo "$(date +'%Y-%m-%d %H:%M:%S') - $1"
}

cd "$SERVER_DIR"
mkdir -p "$BACKUP_DIR"

tgt="$BACKUP_DIR/$(date +%F-%H-%M-%S)"
mkdir "$tgt"

log "Starting PostgreSQL backup..."
docker-compose exec postgres pg_dump -U dual dual > "$tgt/postgres-dual.sql"
log "PostgreSQL 'dual' database backup completed."

docker-compose exec postgres pg_dump -U dual orleans > "$tgt/postgres-orleans.sql"
log "PostgreSQL 'orleans' database backup completed."

log "Starting MongoDB backup..."
docker-compose run --entrypoint mongodump --rm -v "$PWD/$tgt:/output" sandbox -o /output mongodb://mongo:mongo@mongo/
log "MongoDB backup completed."

log "Starting user content backup..."
cp -a data/user_content "$tgt/user_content"
log "User content backup completed."

log "Compressing backup..."
tar -czf "$tgt.tar.gz" -C "$BACKUP_DIR" "$(basename "$tgt")"
log "Backup compression completed."

rm -rf "$tgt"

log "Backup process completed. Compressed backup saved as $tgt.tar.gz"

log "Checking for old backups to remove..."
backup_count=$(ls -1 "$BACKUP_DIR"/*.tar.gz 2>/dev/null | wc -l)
if [ "$backup_count" -gt "$MAX_BACKUPS" ]; then
    num_to_delete=$((backup_count - MAX_BACKUPS))
    log "Removing $num_to_delete old backup(s)..."
    ls -1t "$BACKUP_DIR"/*.tar.gz | tail -n "$num_to_delete" | xargs rm -f
    log "Old backups removed."
else
    log "No old backups need to be removed."
fi
