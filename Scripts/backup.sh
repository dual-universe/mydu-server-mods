#!/bin/bash

log() {
    echo "$(date +'%Y-%m-%d %H:%M:%S') - $1"
}

backup_dir="backup"
mkdir -p "$backup_dir"

tgt="$backup_dir/$(date +%F-%H-%M-%S)"
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
tar -czf "$tgt.tar.gz" -C "$backup_dir" "$(basename "$tgt")"
log "Backup compression completed."

rm -rf "$tgt"

log "Backup process completed. Compressed backup saved as $tgt.tar.gz"