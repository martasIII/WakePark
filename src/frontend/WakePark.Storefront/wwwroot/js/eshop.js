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

    // Věnování: jedno pole na každý kus voucheru. Text z pole, do kterého zákazník psal jako
    // první (lead), se předvyplní do ostatních, dokud je sám nezmění (pak má pole data-own).
    const dedicationFields = () => [...form.querySelectorAll('.dedications textarea')];
    let lead = null;

    const syncDedications = (input) => {
        const box = input.closest('.product').querySelector('.dedications');
        if (!box) {
            return;
        }
        const quantity = Number(input.value) || 0;
        let fields = [...box.querySelectorAll('label')];
        while (fields.length > quantity) {
            fields.pop().remove();
        }
        while (fields.length < quantity) {
            const index = fields.length;
            const label = document.createElement('label');
            label.append(box.dataset.label.replace('{0}', index + 1));
            const area = Object.assign(document.createElement('textarea'), {
                name: `${box.dataset.name}[${index}]`, rows: 2, maxLength: 120,
            });
            area.value = lead?.isConnected ? lead.value : '';
            label.append(area);
            box.insertBefore(label, box.querySelector('.hint'));
            fields.push(label);
        }
        let hint = box.querySelector('.hint');
        if (quantity > 0 && !hint) {
            hint = Object.assign(document.createElement('p'), { className: 'hint', textContent: box.dataset.hint });
            box.append(hint);
        }
        hint?.toggleAttribute('hidden', quantity === 0);
    };

    form.addEventListener('input', (event) => {
        if (!event.target.matches('.dedications textarea')) {
            return;
        }
        if (!lead?.isConnected) {
            lead = event.target;
        }
        if (event.target === lead) {
            for (const field of dedicationFields().filter((f) => f !== lead && !f.dataset.own)) {
                field.value = lead.value;
            }
        } else {
            event.target.dataset.own = 'true';
        }
    });

    const change = (input, delta) => {
        input.value = clamp(input, (Number(input.value) || 0) + delta);
        syncDedications(input);
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
        input.addEventListener('input', () => {
            syncDedications(input);
            update();
        });

        // Klepnutí na obrázek nebo název přidá kus — na mobilu nejrychlejší cesta.
        for (const target of product.querySelectorAll('img, h3')) {
            target.addEventListener('click', () => change(input, +1));
        }
    }

    update();

    // Nákup na firmu: po zadání IČO doplní název, sídlo a DIČ z registru ARES (přes náš server,
    // ARES sám prohlížeč nevolá). Pole jsou jen k nahlédnutí — na doklad je bere server sám.
    const company = form.querySelector('.company');
    if (!company) {
        return;
    }

    const toggle = company.querySelector('.company-toggle input');
    const ico = company.querySelector('input[name="Form.CompanyId"]');
    const status = company.querySelector('.company-status');
    const name = company.querySelector('#company-name');
    const address = company.querySelector('#company-address');
    const vat = company.querySelector('#company-vat');
    let last = ico.value.replace(/\s/g, '');
    let timer;
    let pending;

    const show = (data, message) => {
        name.value = data?.name ?? '';
        address.value = data?.address ?? '';
        vat.value = data?.vatId ?? '';
        vat.placeholder = data && !data.vatId ? vat.dataset.none : '';
        status.textContent = message;
    };

    const lookup = async () => {
        const value = ico.value.replace(/\s/g, '');
        if (value === last) {
            return;
        }
        last = value;
        pending?.abort();

        if (!/^\d{8}$/.test(value)) {
            show(null, value.length > 8 ? status.dataset.notFound : '');
            return;
        }

        show(null, status.dataset.loading);
        pending = new AbortController();
        const url = new URL(location.pathname, location.origin);
        url.searchParams.set('handler', 'Company');
        url.searchParams.set('ico', value);

        try {
            const response = await fetch(url, { signal: pending.signal, headers: { Accept: 'application/json' } });
            if (response.ok) {
                show(await response.json(), '');
            } else {
                show(null, response.status === 404 ? status.dataset.notFound : status.dataset.unavailable);
            }
        } catch (error) {
            if (error.name !== 'AbortError') {
                show(null, status.dataset.unavailable);
            }
        }
    };

    ico.addEventListener('input', () => {
        clearTimeout(timer);
        timer = setTimeout(lookup, 400);
    });
    ico.addEventListener('change', lookup);
    toggle.addEventListener('change', () => {
        ico.required = toggle.checked;
        if (toggle.checked) {
            ico.focus();
        }
    });
})();
