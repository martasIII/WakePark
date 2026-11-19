#!/bin/sh
# Nasazení webu, backoffice, API a databáze na VPS: /opt/wakepark/storefront/deploy.sh (vlastník root, 755).
#
# Spouští ho GitHub Actions přes SSH jako uživatel `deploy`. Klíč je v authorized_keys
# svázaný s tímhle skriptem (command="…", restrict), takže s ním nejde spustit nic jiného
# a nemá shell. Argument přichází v SSH_ORIGINAL_COMMAND:
#   <commit>        produkce  (/opt/wakepark/storefront, větev main)
#   uat <commit>    UAT       (/opt/wakepark/uat, větev uat)
#
# Ručně (jako root): /opt/wakepark/storefront/deploy.sh <commit|latest>
#                    /opt/wakepark/storefront/deploy.sh uat <commit|uat>
#
# Pořadí: databáze → migrace → základní data provozu → katalog → nové kontejnery. Když
# selže migrace nebo import, nová verze se nespustí a dál běží stará.
set -eu

request="${SSH_ORIGINAL_COMMAND:-${*:-latest}}"

# Jen přesně tyhle tvary, na jednom řádku (grep by jinak prošel, kdyby sedl jen jeden z řádků).
# Cokoli jiného by šlo podstrčit do docker compose.
if [ "$(printf '%s' "$request" | wc -l)" -ne 0 ] \
    || ! printf '%s' "$request" | grep -Eq '^(latest|[0-9a-f]{40}|uat (uat|[0-9a-f]{40}))$'; then
    echo "Neplatný požadavek: '$request' (čeká se <commit>, latest, nebo uat <commit>)." >&2
    exit 2
fi

case "$request" in
    uat\ *)
        tag="${request#uat }"
        dir=/opt/wakepark/uat
        web=5083
        backoffice=5084
        # Značka, kterou berou ruční příkazy (uat-refresh.sh, docker compose run …).
        current=uat
        ;;
    *)
        tag="$request"
        dir=/opt/wakepark/storefront
        web=5081
        backoffice=5082
        current=latest
        ;;
esac

cd "$dir"
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
    if curl -fsS "http://127.0.0.1:$web/eshop" 2> /dev/null | grep -q 'class="product"' \
        && [ "$(curl -s -o /dev/null -w '%{http_code}' "http://127.0.0.1:$backoffice/api/v1/identity/me")" = 401 ]; then
        # Ruční příkazy (docker compose run … bez WAKEPARK_TAG) berou :latest, v UAT :uat —
        # ať je to vždy verze, která běží, ne nějaká starší.
        if [ "$tag" != "$current" ]; then
            for service in api storefront backoffice; do
                docker tag "ghcr.io/martasiii/wakepark-$service:$tag" "ghcr.io/martasiii/wakepark-$service:$current"
            done
        fi
        echo "Nasazeno do $dir: $tag"
        exit 0
    fi
    sleep 1
done

echo "Web po 30 s neukazuje nabídku e-shopu, nebo backoffice nedosáhne na API ($dir)." >&2
docker compose ps >&2
docker compose logs --tail 50 api storefront backoffice >&2
exit 1
