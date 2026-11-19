# UAT — testovací prostředí s kopií ostrých dat

Proč a jak: [ADR 0013](../../docs/adr/0013-testovaci-prostredi-uat.md).

| | Adresa | Port na serveru |
|---|---|---|
| Web | https://uat.wake-dzban.cz (jen přihlášený Admin) | 127.0.0.1:5083 |
| Backoffice | https://admin.uat.wake-dzban.cz | 127.0.0.1:5084 |

Na serveru: `/opt/wakepark/uat/` — `docker-compose.yml`, `.env`, `uat-refresh.sh`.

## Běžná práce

**Nasadit do UAT:** push do větve `uat`. CI otestuje, postaví obrazy a nasadí.

```bash
git switch uat
git merge main            # nebo rozpracovaná větev s novou funkcí
git push
```

**Do produkce:** totéž do `main` (`git switch main`, `git merge uat`, `git push`).

**Čerstvá data z produkce** (na serveru jako root; přepíše všechno v UAT):

```bash
/opt/wakepark/uat/uat-refresh.sh --fresh
```

Bez `--fresh` vezme poslední noční zálohu.

**Zastavit / spustit UAT** (šetří paměť, produkce běží dál):

```bash
cd /opt/wakepark/uat && docker compose stop
cd /opt/wakepark/uat && docker compose start
```

## Co je v UAT jinak než na produkci

- žádné párování plateb z Fio, žádné upomínky a rušení nezaplacených objednávek;
- e-maily jen na martin@ a dan@, v předmětu „[UAT → původní adresát]“;
- žlutý pruh na webu, štítek v backoffice, zákaz indexování;
- testovací objednávky **neplatit** — QR ukazuje skutečný účet.

## Zprovoznění (jednou)

1. DNS: A záznamy `uat` a `admin.uat` → 31.31.73.220.
2. Na server `/opt/wakepark/uat/`: `docker-compose.yml`, `uat-refresh.sh` (755) a `.env`
   (root:deploy, 640) s `POSTGRES_PASSWORD` (nové) a `SMTP_PASSWORD` (jako produkce).
3. Nový `deploy.sh` z `deploy/storefront/deploy-on-server.sh` do `/opt/wakepark/storefront/deploy.sh`.
4. nginx: `nginx-uat.wake-dzban.cz.conf` → `/etc/nginx/sites-available/wakepark-uat`,
   odkaz v `sites-enabled`, `nginx -t`, reload, `certbot --nginx -d uat.wake-dzban.cz -d admin.uat.wake-dzban.cz`.
5. Push větve `uat` → první nasazení (prázdná databáze + katalog).
6. `uat-refresh.sh --fresh` → data a účty z produkce.
