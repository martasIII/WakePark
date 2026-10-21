#!/bin/sh
# Denní záloha databáze e-shopu: /opt/wakepark/storefront/backup-db.sh (root, 755),
# spouští cron (/etc/cron.d/wakepark-backup). Drží posledních 14 dní.
#
# Záloha je na stejném serveru. Kopie mimo server se musí vyřešit před spuštěním plateb.
set -eu

target=/var/backups/wakepark
file="$target/wakepark-$(date +%Y-%m-%d).sql.gz"

umask 077
mkdir -p "$target"

# Nejdřív do dočasného souboru: nepovedený dump nesmí přepsat dobrou zálohu ze stejného dne.
# Bez roury — /bin/sh (dash) nezná pipefail a chyba pg_dump by se v rouře ztratila.
docker exec wakepark-db pg_dump -U wakepark -d wakepark --clean --if-exists > "$target/dump.tmp"
gzip -c "$target/dump.tmp" > "$file.tmp"
rm "$target/dump.tmp"
mv "$file.tmp" "$file"

find "$target" -name 'wakepark-*.sql.gz' -mtime +14 -delete
