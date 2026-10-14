# Nasazení veřejného webu na VPS

Web běží na Wedos VPS (Debian 12, 1 jádro, 2 GB RAM) vedle Docházky:

```
internet ─► nginx (80/443, certifikáty Let's Encrypt)
              ├─ dochazka.wake-dzban.cz ─► 127.0.0.1:5080  Docházka (systemd)
              └─ novy.wake-dzban.cz     ─► 127.0.0.1:5081  Storefront (Docker)
```

Nasazení: `.\deploy\storefront\deploy.ps1` (postaví obraz lokálně a přenese ho přes SSH).
Později ho převezme GitHub Actions.

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

### 5. První nasazení

Z vývojového počítače: `.\deploy\storefront\deploy.ps1`, pak ověřit https://novy.wake-dzban.cz.

## Údržba

| Co | Příkaz na serveru |
|---|---|
| Stav | `cd /opt/wakepark/storefront && docker compose ps` |
| Logy | `docker compose logs --tail 100 -f` |
| Restart | `docker compose restart` |
| Spotřeba paměti | `docker stats --no-stream` |
