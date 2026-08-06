#!/usr/bin/env bash
# Dumps the production database, compressed, and prunes dumps older than a week.
# Installed to /usr/local/bin and driven by a systemd timer — see setup-backup.sh.
set -euo pipefail

ENV_FILE="${ENV_FILE:-/home/hung/deploy/stayhost.env}"
DEST="${DEST:-/home/hung/backups}"
KEEP_DAYS="${KEEP_DAYS:-7}"
CONTAINER="${CONTAINER:-stayhost-db}"

# shellcheck disable=SC1090
set -a; . "$ENV_FILE"; set +a
DB="${POSTGRES_DB:-stayhost}"
DB_USER="${POSTGRES_USER:-stayhost}"

mkdir -p "$DEST"
OUT="$DEST/stayhost-$(date +%F-%H%M).sql.gz"
TMP="$OUT.partial"
trap 'rm -f "$TMP"' EXIT

# Write under a .partial name first: a crash mid-dump must never leave a truncated
# file sitting there looking like a usable backup.
docker exec "$CONTAINER" pg_dump -U "$DB_USER" -d "$DB" --clean --if-exists \
  | gzip -c > "$TMP"

gzip -t "$TMP"
SIZE="$(stat -c %s "$TMP")"
if [ "$SIZE" -lt 10240 ]; then
  echo "Dump is only ${SIZE} bytes — refusing to keep it." >&2
  exit 1
fi

mv "$TMP" "$OUT"
trap - EXIT
echo "Wrote $OUT ($(numfmt --to=iec "$SIZE"))"

# Prune by age, but only once a fresh dump is safely on disk.
find "$DEST" -maxdepth 1 -name 'stayhost-*.sql.gz' -type f -mtime "+${KEEP_DAYS}" -print -delete
find "$DEST" -maxdepth 1 -name 'stayhost-*.partial' -type f -mtime +1 -delete

echo "Kept $(find "$DEST" -maxdepth 1 -name 'stayhost-*.sql.gz' | wc -l) backup(s), $(du -sh "$DEST" | cut -f1) total."
