// Výběr počtu kusů na stránce e-shopu. Bez skriptu formulář funguje dál (políčko s číslem);
// skript jen přidá tlačítka − / +, přičtení kusu klepnutím na kartu a průběžný součet.
(() => {
    const form = document.querySelector('form.order-form');
    if (!form) {
        return;
    }

    const inputs = [...form.querySelectorAll('.product input[type=number]')];
    const total = form.querySelector('.order-total');
    const totalAmount = total?.querySelector('[data-amount]');
    const formatter = new Intl.NumberFormat(document.documentElement.lang || 'cs', {
        style: 'currency', currency: 'CZK', maximumFractionDigits: 0,
    });

    const clamp = (input, value) =>
        Math.min(Number(input.max) || 20, Math.max(Number(input.min) || 0, value));

    const update = () => {
        let sum = 0;
        for (const input of inputs) {
            const quantity = Number(input.value) || 0;
            sum += quantity * Number(input.dataset.price);
            input.closest('.product').classList.toggle('selected', quantity > 0);
        }
        if (total) {
            total.hidden = sum === 0;
            totalAmount.textContent = formatter.format(sum);
        }
    };

    const change = (input, delta) => {
        input.value = clamp(input, (Number(input.value) || 0) + delta);
        update();
    };

    for (const input of inputs) {
        const product = input.closest('.product');
        const minus = Object.assign(document.createElement('button'), { type: 'button', className: 'step', textContent: '−' });
        const plus = Object.assign(document.createElement('button'), { type: 'button', className: 'step', textContent: '+' });
        minus.setAttribute('aria-label', input.dataset.less);
        plus.setAttribute('aria-label', input.dataset.more);
        minus.addEventListener('click', () => change(input, -1));
        plus.addEventListener('click', () => change(input, +1));
        input.before(minus);
        input.after(plus);
        input.addEventListener('input', update);

        // Klepnutí na obrázek nebo název přidá kus — na mobilu nejrychlejší cesta.
        for (const target of product.querySelectorAll('img, h3')) {
            target.addEventListener('click', () => change(input, +1));
        }
    }

    update();
})();
