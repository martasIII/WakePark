-- Úklid testovacích dat před ostrým spuštěním e-shopu (spustit JEDNOU, těsně před spuštěním).
--
--   /opt/wakepark/storefront/backup-db.sh          ← nejdřív záloha!
--   docker exec -i wakepark-db psql -U wakepark -d wakepark -v ON_ERROR_STOP=1 < cleanup-test-data.sql
--
-- Smaže testovací objednávky, vouchery, e-maily a události mezi moduly a vynuluje číselné
-- řady: první ostrá objednávka bude 261000 (resp. RR1000), první doklad FV_E_rrrr-000001
-- (rozhodnutí účetní 22. 10. 2026: nová předpona = nová řada).
--
-- Bankovní pohyby ZŮSTÁVAJÍ: jsou to skutečné platby z účtu a podle jejich ID párování
-- pozná, že je už zpracovalo. Kdyby se smazaly, stáhly by se znovu (7 dní zpět) a přišla by
-- upozornění „platba k ručnímu vyřízení“ k objednávkám, které už neexistují.

BEGIN;

-- E-maily (potvrzení, vouchery, upomínky, upozornění)
DELETE FROM notifications.email_attachments;
DELETE FROM notifications.emails;

-- Vouchery a jejich události
DELETE FROM vouchers.outbox_messages;
DELETE FROM vouchers.vouchers;

-- Daňové doklady z testů (FV2026-00001 …). Pokud je účetní místo smazání stornuje,
-- zakomentuj následující dva řádky — číselná řada se vynuluje tak jako tak.
DELETE FROM billing.invoice_lines;
DELETE FROM billing.invoices;
DELETE FROM billing.invoice_number_series;

-- Objednávky a události Billingu
DELETE FROM billing.outbox_messages;
DELETE FROM billing.order_lines;
DELETE FROM billing.orders;
DELETE FROM billing.order_number_series;

-- Pohyby zůstávají, jen bez odkazu na smazané objednávky.
UPDATE billing.bank_transactions SET order_id = NULL WHERE order_id IS NOT NULL;

-- Kontrola: všechno, co se mělo smazat, je prázdné.
SELECT 'orders' AS tabulka, count(*) FROM billing.orders
UNION ALL SELECT 'invoices', count(*) FROM billing.invoices
UNION ALL SELECT 'vouchers', count(*) FROM vouchers.vouchers
UNION ALL SELECT 'emails', count(*) FROM notifications.emails;

COMMIT;
