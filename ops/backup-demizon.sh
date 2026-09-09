#!/usr/bin/env bash
#
# Záloha Demizonu ze Scaleway Stardust hostitele.
#
# Fotky jsou BLOBy uvnitř SQLite, takže tenhle jeden soubor je celý obsah
# aplikace. `cp` nad běžící databází ale dá nekonzistentní kopii — WAL může
# obsahovat potvrzené transakce, které v hlavním souboru ještě nejsou.
# Proto `.backup`, což je online backup API: projde i za běhu appky a
# kontejner není potřeba zastavovat.
#
# Spouští se na hostiteli (image je runtime-only a `sqlite3` v něm není).
# Viz docs/nasazeni.md, sekce „Záloha /data“.
#
set -euo pipefail

DB="${DEMIZON_DB:-/var/lib/docker/volumes/demizon-data/_data/demizon.sqlite}"
DEST="${DEMIZON_BACKUP_DIR:-/var/backups/demizon}"
# Záměrně málo: disk má 10 GB a kvóta na uploady připouští 2 GB dat, takže
# víc lokálních kopií se tam nevejde. Delší historie patří mimo stroj.
KEEP_DAYS="${DEMIZON_BACKUP_KEEP_DAYS:-3}"

STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
DATA_DIR="$(dirname "$DB")"
SNAPSHOT="$DEST/demizon-$STAMP.sqlite"

fail() { echo "backup-demizon: $*" >&2; exit 1; }

command -v sqlite3 >/dev/null 2>&1 || fail "sqlite3 není nainstalovaný (apt install sqlite3)"
[ -f "$DB" ] || fail "databáze nenalezena: $DB"

mkdir -p "$DEST"

sqlite3 "$DB" ".backup '$SNAPSHOT'" || fail "sqlite3 .backup selhal"

# Kontrola nad kopií, ne nad produkcí. Záloha, kterou nejde otevřít, není záloha
# a chce se to zjistit teď, ne až při obnově.
integrity="$(sqlite3 "$SNAPSHOT" 'PRAGMA integrity_check;')"
[ "$integrity" = "ok" ] || { rm -f "$SNAPSHOT"; fail "integrity_check: $integrity"; }

gzip -9 "$SNAPSHOT"

# DataProtection klíče. Bez nich se po obnově odhlásí všichni přihlášení —
# není to ztráta dat, ale je to zbytečné překvapení.
if [ -d "$DATA_DIR/keys" ]; then
    tar -czf "$DEST/demizon-keys-$STAMP.tar.gz" -C "$DATA_DIR" keys
fi

# Retence lokálních kopií.
find "$DEST" -maxdepth 1 -name 'demizon-*.gz' -type f -mtime "+$KEEP_DAYS" -delete

# Kopie mimo stroj. Jeden mrtvý disk jinak znamená konec dat, takže bez tohohle
# kroku je celý skript jen ochrana proti překlepu, ne proti hardwaru.
if [ -n "${DEMIZON_BACKUP_REMOTE:-}" ]; then
    command -v rclone >/dev/null 2>&1 || fail "DEMIZON_BACKUP_REMOTE je nastavené, ale rclone chybí"
    rclone copy "$DEST" "$DEMIZON_BACKUP_REMOTE" --include 'demizon-*.gz' \
        || fail "rclone copy selhal"
else
    echo "backup-demizon: VAROVÁNÍ – DEMIZON_BACKUP_REMOTE není nastavené, záloha zůstává na stejném stroji" >&2
fi

echo "backup-demizon: hotovo $SNAPSHOT.gz"
