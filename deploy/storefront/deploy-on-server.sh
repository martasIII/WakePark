#!/bin/sh
# Nasazení webu, backoffice, API a databáze na VPS: /opt/wakepark/storefront/deploy.sh (vlastník root, 755).
#
# Spouští ho GitHub Actions přes SSH jako uživatel `deploy`. Klíč je v authorized_keys
# svázaný s tímhle skriptem (command="…", restrict), takže s ním nejde spustit nic jiného
# a nemá shell. Argument (commit, který se má nasadit) přichází v SSH_ORIGINAL_COMMAND.
#
# Ručně (jako root): /opt/wakepark/storefront/deploy.sh <commit|latest>
#
# Pořadí: databáze → migrace → základní data provozu → katalog → nové kontejnery. Když
# selže migrace nebo import, nová verze se nespustí a dál běží stará.
set -eu

tag="${SSH_ORIGINAL_COMMAND:-${1:-latest}}"

# Jen celý hash commitu nebo latest. Cokoli jiného by šlo podstrčit do docker compose.
if ! printf '%s' "$tag" | grep -Eq '^(latest|[0-9a-f]{40})$'; then
    echo "Neplatná verze: '$tag' (čeká se 40znakový hash commitu nebo latest)." >&2
    exit 2
fi

cd /opt/wakepark/storefront
export WAKEPARK_TAG="$tag"

docker compose pull --quiet
docker compose up -d --wait db
docker compose run --rm --no-deps api migrate
docker compose run --rm --no-deps api import-setup
docker compose run --rm --no-deps api import-catalog
docker compose up -d --remove-orphans
docker image prune -f > /dev/null

# Hotovo je, až web ukáže nabídku (web → API → databáze) a backoffice dosáhne na API
# (nepřihlášenému vrátí 401, ne 502 od svého nginxu).
for _ in $(seq 1 30); do
    if curl -fsS http://127.0.0.1:5081/eshop 2> /dev/null | grep -q 'class="product"'         && [ "$(curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:5082/api/v1/identity/me)" = 401 ]; then
        # Ruční příkazy z README (docker compose run … bez WAKEPARK_TAG) berou :latest —
        # ať je to vždy verze, která běží, ne nějaká starší.
        if [ "$tag" != latest ]; then
            for service in api storefront backoffice; do
                docker tag "ghcr.io/martasiii/wakepark-$service:$tag" "ghcr.io/martasiii/wakepark-$service:latest"
            done
        fi
        echo "Nasazeno: $tag"
        exit 0
    fi
    sleep 1
done

echo "Web po 30 s neukazuje nabídku e-shopu, nebo backoffice nedosáhne na API." >&2
docker compose ps >&2
docker compose logs --tail 50 api storefront backoffice >&2
exit 1
