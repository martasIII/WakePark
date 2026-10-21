// Stránka objednávky, která čeká na platbu, se každých 30 s zeptá na stav a po zaplacení se
// sama obnoví. Ptá se jen v otevřené (viditelné) záložce a nejvýš 4 hodiny — pak už zákazník
// stránku nesleduje a obnoví ji sám. Bez skriptu stačí stránku obnovit ručně.
(() => {
    const watcher = document.querySelector('[data-status-url]');
    if (!watcher) {
        return;
    }

    const url = watcher.dataset.statusUrl;
    const until = Date.now() + 4 * 60 * 60 * 1000;

    const check = async () => {
        if (Date.now() > until) {
            clearInterval(timer);
            return;
        }
        if (document.visibilityState !== 'visible') {
            return;
        }
        try {
            const response = await fetch(url, { cache: 'no-store' });
            if (response.ok && (await response.json()).status !== 'AwaitingPayment') {
                clearInterval(timer);
                location.reload();
            }
        } catch {
            // Výpadek sítě nevadí, zkusí se to příště.
        }
    };

    const timer = setInterval(check, 30 * 1000);
    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'visible') {
            check();
        }
    });
})();
