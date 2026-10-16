#!/bin/sh
# Nasazení veřejného webu na VPS: /opt/wakepark/storefront/deploy.sh (vlastník root, 755).
#
# Spouští ho GitHub Actions přes SSH jako uživatel `deploy`. Klíč je v authorized_keys
# svázaný s tímhle skriptem (command="…", restrict), takže s ním nejde spustit nic jiného
# a nemá shell. Argument (commit, který se má nasadit) přichází v SSH_ORIGINAL_COMMAND.
#
# Ručně (jako root): /opt/wakepark/storefront/deploy.sh <commit|latest>
set -eu

tag="${SSH_ORIGINAL_COMMAND:-${1:-latest}}"

# Jen celý hash commitu nebo latest. Cokoli jiného by šlo podstrčit do docker compose.
if ! printf '%s' "$tag" | grep -Eq '^(latest|[0-9a-f]{40})$'; then
    echo "Neplatná verze: '$tag' (čeká se 40znakový hash commitu nebo latest)." >&2
    exit 2
fi

cd /opt/wakepark/storefront
export STOREFRONT_TAG="$tag"

docker compose pull --quiet
docker compose up -d --remove-orphans
docker image prune -f > /dev/null

for _ in $(seq 1 30); do
    if curl -fsS http://127.0.0.1:5081/health > /dev/null 2>&1; then
        echo "Nasazeno: $tag"
        exit 0
    fi
    sleep 1
done

echo "Web po 30 s neodpovídá na /health." >&2
docker compose logs --tail 50 >&2
exit 1
