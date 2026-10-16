# Nasazení veřejného webu na VPS

Web běží na Wedos VPS (Debian 12, 1 jádro, 2 GB RAM) vedle Docházky:

```
internet ─► nginx (80/443, certifikáty Let's Encrypt)
              ├─ dochazka.wake-dzban.cz ─► 127.0.0.1:5080  Docházka (systemd)
              └─ novy.wake-dzban.cz     ─► 127.0.0.1:5081  Storefront (Docker)
```

## Jak se nasazuje

Samo, po každém pushi do `main`, když projdou testy (`.github/workflows/ci.yml`):

```
push → CI: testy ✔ + obraz ghcr.io/martasiii/wakepark-storefront:<commit>
     → značka latest → ssh deploy@VPS "<commit>" → deploy.sh: pull, up, kontrola /health
```

Uživatel `deploy` na serveru nemá heslo ani shell. Jeho klíč (v GitHub Secrets) je
v `authorized_keys` svázaný s jediným skriptem `/opt/wakepark/storefront/deploy.sh`
(`restrict,command="…"`) — nespustí nic jiného, neotevře tunel a jako argument přijme jen
hash commitu nebo `latest`. Skript i compose patří rootovi, `deploy` je nepřepíše.

**Návrat ke starší verzi** (jako root na serveru): `/opt/wakepark/storefront/deploy.sh <hash commitu>`.

**Záloha, když nejde GitHub:** `.\deploy\storefront\deploy.ps1` postaví obraz lokálně
a nasadí ho se značkou `manual`.

**Změna `docker-compose.yml`** se na server nenasazuje sama (klíč pro CI smí jen spustit
skript) — po úpravě ho zkopíruj ručně: `scp deploy/storefront/docker-compose.yml dochazka:/opt/wakepark/storefront/`.

## Jednorázové nastavení serveru

Spouští se jako root přes `ssh dochazka`. Každý krok se dá zkontrolovat, než se jde dál.

### 1. Docker z oficiálního repozitáře

Balík `docker.io` z Debianu je zastaralý a nemá `docker compose` v2.

```bash
apt-get update
apt-get install -y ca-certificates curl
install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/debian/gpg -o /etc/apt/keyrings/docker.asc
chmod a+r /etc/apt/keyrings/docker.asc
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/debian $(. /etc/os-release && echo "$VERSION_CODENAME") stable" > /etc/apt/sources.list.d/docker.list
apt-get update
apt-get install -y docker-ce docker-ce-cli containerd.io docker-compose-plugin
docker version && docker compose version
```

### 2. Složka pro compose

```bash
mkdir -p /opt/wakepark/storefront
```

### 3. nginx: výchozí server už neposílá všechno na Docházku

Původní `default_server` v `/etc/nginx/sites-enabled/dochazka` posílal **jakoukoli** doménu
i holou IP na Docházku po nešifrovaném HTTP. Nově neznámé požadavky zahodí (444 = zavřít
spojení bez odpovědi). Docházka na `dochazka.wake-dzban.cz` funguje dál beze změny.

```bash
cp /etc/nginx/sites-available/dochazka /root/dochazka.nginx.zaloha-$(date +%F)
# v /etc/nginx/sites-available/dochazka nahradit první blok server { listen 80 default_server … }
# blokem:
#   server {
#       listen 80 default_server;
#       listen [::]:80 default_server;
#       server_name _;
#       return 444;
#   }
nginx -t && systemctl reload nginx
```

### 4. nginx: nový web a certifikát

DNS záznam `novy.wake-dzban.cz` musí už ukazovat na `31.31.73.220`, jinak certbot neprojde.

```bash
# obsah deploy/storefront/nginx-novy.wake-dzban.cz.conf
nano /etc/nginx/sites-available/storefront
ln -s /etc/nginx/sites-available/storefront /etc/nginx/sites-enabled/storefront
nginx -t && systemctl reload nginx
certbot --nginx -d novy.wake-dzban.cz --redirect
```

Certbot doplní HTTPS a přesměrování z HTTP. Obnovu certifikátu už řeší `certbot.timer`,
který běží kvůli Docházce.

### 5. Uživatel pro nasazení z GitHubu

Klíčový pár se generuje na vývojovém počítači (`ssh-keygen -t ed25519 -N "" -f deploy_key`),
**soukromý klíč jde jen do GitHub Secrets**, na disku nezůstává.

```bash
useradd --create-home --shell /bin/sh --groups docker deploy
passwd -l deploy
install -o root -g root -m 755 deploy-on-server.sh /opt/wakepark/storefront/deploy.sh
install -o deploy -g deploy -m 700 -d /home/deploy/.ssh
echo 'restrict,command="/opt/wakepark/storefront/deploy.sh" ssh-ed25519 AAAA… github-actions-deploy@wakepark' \
  > /home/deploy/.ssh/authorized_keys
chown deploy:deploy /home/deploy/.ssh/authorized_keys && chmod 600 /home/deploy/.ssh/authorized_keys
```

Členství ve skupině `docker` je fakticky ekvivalent roota — proto je klíč svázaný
se skriptem a nikdy se nepoužívá k ničemu jinému.

GitHub → Settings → Secrets and variables → Actions:

| Secret | Obsah |
|---|---|
| `VPS_DEPLOY_SSH_KEY` | soukromý klíč `deploy_key` (celý, včetně řádků BEGIN/END) |
| `VPS_KNOWN_HOSTS` | výstup `ssh-keyscan 31.31.73.220`, ověřený proti známému otisku serveru |

Obraz v ghcr.io musí být **veřejný** (GitHub → Packages → wakepark-storefront → Package
settings → Change visibility), jinak by si ho server nestáhl bez přihlášení.

### 6. První nasazení

Push do `main` → v GitHubu záložka Actions → úloha „Storefront — nasazení na VPS“.

## Údržba

| Co | Příkaz na serveru |
|---|---|
| Stav | `cd /opt/wakepark/storefront && docker compose ps` |
| Logy | `docker compose logs --tail 100 -f` |
| Restart | `docker compose restart` |
| Spotřeba paměti | `docker stats --no-stream` |
