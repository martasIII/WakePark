#!/bin/sh
# Nahraje do UAT kopii ostrých dat: /opt/wakepark/uat/uat-refresh.sh (root, 755).
#
#   ./uat-refresh.sh           poslední noční záloha produkce (/var/backups/wakepark)
#   ./uat-refresh.sh --fresh   nejdřív udělá novou zálohu produkce, pak ji nahraje
#
# Všechno, co bylo v UAT (testovací objednávky, změny), se přepíše. Produkce se jen čte
# (pg_dump), nic se v ní nemění. Účty a hesla jsou po obnově stejné jako na produkci.
set -eu

cd /opt/wakepark/uat

if [ "${1:-}" = "--fresh" ]; then
    /opt/wakepark/storefront/backup-db.sh
fi

backup=$(ls -1t /var/backups/wakepark/wakepark-*.sql.gz 2> /dev/null | head -n 1)
if [ -z "$backup" ]; then
    echo "Nenašel jsem žádnou zálohu v /var/backups/wakepark." >&2
    exit 1
fi
echo "Obnovuji UAT ze zálohy $backup"

# Rozbalení zvlášť, ne rourou — /bin/sh (dash) nezná pipefail a chyba by se ztratila.
umask 077
tmp=$(mktemp)
trap 'rm -f "$tmp"' EXIT
gunzip -c "$backup" > "$tmp"

# Web a API stojí, ať během obnovy nikdo nepíše do napůl nahrané databáze.
docker compose stop api storefront
docker compose up -d --wait db
docker exec -i wakepark-uat-db psql -q -v ON_ERROR_STOP=1 -U wakepark -d wakepark < "$tmp" > /dev/null

# Kód v UAT bývá novější než produkce — dorovnat schéma, základní data a katalog.
docker compose run --rm --no-deps api migrate
docker compose run --rm --no-deps api import-setup
docker compose run --rm --no-deps api import-catalog
docker compose up -d

echo "Hotovo: UAT má data ze zálohy $(basename "$backup")."
