# Nasazení webu a e-shopu na VPS

Web běží na Wedos VPS (Debian 12, 1 jádro, 2 GB RAM) vedle Docházky:

```
internet ─► nginx (80/443, certifikáty Let's Encrypt)
              ├─ dochazka.wake-dzban.cz ─► 127.0.0.1:5080  Docházka (systemd)
              └─ novy.wake-dzban.cz     ─► 127.0.0.1:5081  Storefront (Docker)
                                                          │ http://api:8080 (síť Dockeru)
                                                          ▼
                                                        API ─► PostgreSQL (svazek pgdata)
```

API ani databáze nemají publikovaný port, z internetu ani z hostitele na ně nejde sáhnout.
Heslo k databázi je jen v `/opt/wakepark/storefront/.env` (root:deploy, 640 — `deploy.sh` ho potřebuje číst).

## Jak se nasazuje

Samo, po každém pushi do `main`, když projdou testy (`.github/workflows/ci.yml`):

```
push → CI: testy ✔ + obrazy ghcr.io/martasiii/wakepark-{storefront,api}:<commit>
     → značka latest → ssh deploy@VPS "<commit>"
     → deploy.sh: pull → databáze → migrace → import katalogu → nové kontejnery
                  → kontrola, že /eshop ukazuje nabídku (web → API → databáze)
```

Uživatel `deploy` na serveru nemá heslo ani shell. Jeho klíč (v GitHub Secrets) je
v `authorized_keys` svázaný s jediným skriptem `/opt/wakepark/storefront/deploy.sh`
(`restrict,command="…"`) — nespustí nic jiného, neotevře tunel a jako argument přijme jen
hash commitu nebo `latest`. Skript i compose patří rootovi, `deploy` je nepřepíše.

**Návrat ke starší verzi** (jako root na serveru): `/opt/wakepark/storefront/deploy.sh <hash commitu>`.

Když selže migrace nebo import katalogu, skript skončí dřív, než spustí nové kontejnery —
dál běží stará verze. Migrace proto musí být zpětně kompatibilní (stará verze chvíli
běží proti novému schématu).

**Záloha, když nejde GitHub:** `.\deploy\storefront\deploy.ps1` postaví oba obrazy lokálně
a nasadí je se značkou `manual`.

**Změna `docker-compose.yml` nebo `deploy-on-server.sh`** se na server nenasazuje sama
(klíč pro CI smí jen spustit skript) — po úpravě je zkopíruj ručně:

```bash
scp deploy/storefront/docker-compose.yml dochazka:/opt/wakepark/storefront/
scp deploy/storefront/deploy-on-server.sh dochazka:/opt/wakepark/storefront/deploy.sh
```

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

### 6. Swap, heslo k databázi, zálohy

Server nemá swap; když dojde paměť, kernel zabíjí procesy — klidně i Docházku. 1 GB swapu
je pojistka, ne místo pro běžný provoz.

```bash
fallocate -l 1G /swapfile && chmod 600 /swapfile && mkswap /swapfile && swapon /swapfile
echo '/swapfile none swap sw 0 0' >> /etc/fstab
echo 'vm.swappiness=10' > /etc/sysctl.d/90-swappiness.conf && sysctl --system
```

Heslo k databázi (vygeneruje se jednou, nikam jinam se nepíše):

```bash
umask 077
printf 'POSTGRES_PASSWORD=%s\n' "$(openssl rand -hex 24)" > /opt/wakepark/storefront/.env
# deploy.sh běží jako uživatel deploy a compose si z .env heslo čte
chown root:deploy /opt/wakepark/storefront/.env && chmod 640 /opt/wakepark/storefront/.env
```

Denní záloha databáze (`pg_dump`, 14 dní zpětně) do `/var/backups/wakepark/`:

```bash
install -m 700 -d /var/backups/wakepark
install -o root -g root -m 755 backup-db.sh /opt/wakepark/storefront/backup-db.sh
echo '30 3 * * * root /opt/wakepark/storefront/backup-db.sh' > /etc/cron.d/wakepark-backup
```

Na serveru pomůže záloha při chybě aplikace nebo omylem smazaných datech, ne když umře
server. **Kopie mimo server:** `scripts/backup-from-server.ps1` na Martinově počítači každý den
ve 12:00 (Plánovač úloh Windows „WakePark - zaloha databaze“, zmeškaný běh dožene po zapnutí)
spustí zálohu na serveru, ověří ji (`gzip -t`, velikost) a stáhne do
`%USERPROFILE%\Zalohy\WakePark` — mimo OneDrive, zálohy obsahují osobní údaje. Drží 60 dní,
nejnovějších 10 vždy. Průběh v `backup.log` tamtéž.

Obnova (přepíše současná data!):

```bash
gunzip -c /var/backups/wakepark/wakepark-<datum>.sql.gz \
  | docker exec -i wakepark-db psql -U wakepark -d wakepark
```

### 7. První nasazení

Push do `main` → v GitHubu záložka Actions → úloha „Storefront — nasazení na VPS“.

## Ostré spuštění (přepnutí na wake-dzban.cz)

Pořadí je důležité. Odhad: hodina práce, web je nedostupný jen pár minut kolem kroku 5.

**Den předem**
1. Ve Wedosu snížit TTL A záznamů `wake-dzban.cz` a `www` na **300 s** (dnes 3600) —
   přepnutí se pak projeví do 5 minut, ne do hodiny.
2. V repu (commit + push, nasadí se samo):
   - finální katalog a ceny: v backoffice, nebo `katalog.json` + obrázky a po nasazení
     jednou `import-catalog --overwrite` (nasazení samo jen doplní chybějící produkty),
   - finální text obchodních podmínek na stránce `Obchodni-podminky.cshtml`,
   - `Storefront:EshopTrialMode` → `false` (zmizí pruh „Zkušební provoz“),
   - v `docker-compose.yml` u api `Notifications__PublicBaseUrl: "https://wake-dzban.cz"`
     (odkazy v e-mailech) — compose zkopírovat na server (viz výše).

**V den spuštění** (jako root, `ssh dochazka`)
3. Záloha a úklid testovacích dat (objednávky, doklady, vouchery, e-maily, číselné řady):
   ```bash
   /opt/wakepark/storefront/backup-db.sh
   docker exec -i wakepark-db psql -U wakepark -d wakepark -v ON_ERROR_STOP=1 < cleanup-test-data.sql
   ```
4. Ve Wedosu změnit A záznamy `wake-dzban.cz` a `www.wake-dzban.cz` na **31.31.73.220**.
   **MX ani TXT (SPF, DMARC) neměnit** — pošta zůstává u Wedosu.
5. Až `dig +short wake-dzban.cz` vrací 31.31.73.220:
   ```bash
   install -m 644 nginx-wake-dzban.cz.conf /etc/nginx/sites-available/wake-dzban
   ln -s /etc/nginx/sites-available/wake-dzban /etc/nginx/sites-enabled/wake-dzban
   nginx -t && systemctl reload nginx
   certbot --nginx -d wake-dzban.cz -d www.wake-dzban.cz --redirect
   ```
6. `novy.wake-dzban.cz` přesměrovat na hlavní doménu se zachováním cesty — odkazy
   v už odeslaných e-mailech (`/objednavka/…`) musí dál fungovat. V
   `/etc/nginx/sites-available/storefront` v bloku pro 443 nahradit `location / {…}` za
   `return 301 https://wake-dzban.cz$request_uri;`, pak `nginx -t && systemctl reload nginx`.
7. Zkontrolovat: https://wake-dzban.cz, /eshop, /en, staré adresy (`/cenik.html`, `/shop/`),
   jedna objednávka za 1 Kč nanečisto (pak stornovat ručně).
8. UptimeRobot přepnout na `https://wake-dzban.cz/eshop`.

**Potom**
- Statický web na webhostingu Wedos nechat pár týdnů jako zálohu, pak zrušit.
- Písmo Montserrat hostovat na vlastním serveru místo Google Fonts (zásady ochrany
  osobních údajů pak nemusí zmiňovat předání IP adresy Googlu).

## Backoffice: admini a provoz (ADR 0009)

**Základní data provozu** (provozovatel, areál, vleky, nastavení) jsou v
`src/backend/WakePark.Api/data/provoz.json`. Každé nasazení je srovná krokem `import-setup`
— úprava vleku = úprava souboru → commit → nasazení.

**Admin** (jako root na serveru, opakovatelné; heslo se zadá v terminálu a nikde jinde není):

```bash
cd /opt/wakepark/storefront && docker compose run --rm -it --no-deps api create-admin jmeno@wake-dzban.cz
```

První admin se stane vlastníkem provozovatele. Existujícímu účtu příkaz jen přidá roli admina,
heslo nemění.

**Otevírací doba z backoffice na webu:** po nahrání CSV sezóny (backoffice nebo
`POST /api/v1/operations/parks/{id}/days/import`) přepnout v `docker-compose.yml`
`Storefront__OpeningHoursFromApi` na `"true"`, compose zkopírovat na server a
`docker compose up -d storefront`. Do té doby web čte `oteviraci-doba.json`.

## Údržba

| Co | Příkaz na serveru |
|---|---|
| Stav | `cd /opt/wakepark/storefront && docker compose ps` |
| Logy | `docker compose logs --tail 100 -f` |
| Restart | `docker compose restart` |
| Spotřeba paměti | `docker stats --no-stream` |
| Databáze (psql) | `docker exec -it wakepark-db psql -U wakepark -d wakepark` |
| Ruční záloha | `/opt/wakepark/storefront/backup-db.sh` |
| Doplnit chybějící z `katalog.json` | `docker compose run --rm api import-catalog` (dělá i každé nasazení) |
| Přepsat katalog souborem | `docker compose run --rm api import-catalog --overwrite` — **přepíše i úpravy z backoffice** |
