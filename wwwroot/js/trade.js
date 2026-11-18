// Only integer minor units are used for the preview; the server validates and recomputes each total.
(() => {
    'use strict';
    const french = new Intl.NumberFormat('fr-CD');
    const cents = value => {
        const text = String(value ?? '').trim();
        if (text.length > 64) return null;
        const match = text.match(/^(\d+|\d{1,3}(?:[ \u00a0\u202f]\d{3})+)(?:[.,](\d+))?$/);
        if (!match) return null;
        const fraction = (match[2] || '').replace(/0+$/, '');
        return fraction.length <= 2
            ? BigInt(match[1].replace(/[ \u00a0\u202f]/g, '')) * 100n + BigInt(fraction.padEnd(2, '0'))
            : null;
    };
    const display = (amount, currency) => `${french.format(amount / 100n)},${String(amount % 100n).padStart(2, '0')} ${currency}`;

    if (typeof module !== 'undefined' && module.exports) module.exports = { cents, display };
    if (typeof document === 'undefined') return;

    document.querySelectorAll('form[data-trade]').forEach(form => {
        const list = form.querySelector('[data-trade-lines]');
        const template = form.querySelector('[data-line-template]');
        const add = form.querySelector('[data-add-line]');
        const currency = form.querySelector('[data-trade-currency]');
        const totalView = form.querySelector('[data-trade-total]');
        const expected = form.querySelector('[data-expected-total]');
        const discountInput = form.querySelector('[data-trade-discount]');
        const mode = form.querySelector('[data-payment-mode]');
        const max = Number(list.dataset.maxLines);
        const rows = () => Array.from(list.querySelectorAll('[data-trade-line]'));

        function update() {
            const code = currency.value;
            const lines = rows();
            let subtotal = 0n;
            let valid = true;
            for (const line of lines) {
                const product = line.querySelector('[data-role="product"]');
                const selected = product.selectedOptions[0];
                const quantity = line.querySelector('[data-role="quantity"]').value;
                const input = line.querySelector('[data-role="price"]');
                const price = cents(input ? input.value : selected?.getAttribute(`data-price-${code.toLowerCase()}`));
                const read = line.querySelector('[data-read-price]');
                if (read) read.textContent = price == null ? '' : `${display(price, code)} / unité`;
                if (!product.value || !/^\d{1,6}$/.test(quantity) || BigInt(quantity) < 1n || price == null) valid = false;
                else subtotal += BigInt(quantity) * price;
            }
            const discount = cents(discountInput?.value || '0');
            const total = subtotal - (discount ?? 0n);
            valid = valid && discount != null && total >= 0n;
            if (totalView) totalView.textContent = valid ? display(total, code) : '—';
            if (expected) expected.value = valid ? `${total / 100n}.${String(total % 100n).padStart(2, '0')}` : '';
            if (mode) {
                const partial = form.querySelector('[data-partial-payment]');
                const method = form.querySelector('[data-payment-method]');
                partial.hidden = mode.value !== 'Partial';
                partial.querySelector('input').disabled = partial.hidden;
                method.hidden = mode.value === 'Unpaid';
                method.querySelector('select').disabled = method.hidden;
            }
        }
        function reindex() {
            const lines = rows();
            lines.forEach((line, index) => {
                line.querySelectorAll('[data-field]').forEach(field => {
                    field.name = `Items[${index}].${field.dataset.field}`;
                    field.id = `line-${index}-${field.dataset.role}`;
                });
                line.querySelectorAll('[data-label]').forEach(label => { label.htmlFor = `line-${index}-${label.dataset.label}`; });
                line.querySelector('[data-remove-line]').disabled = lines.length < 2;
            });
            add.disabled = lines.length >= max;
            update();
        }
        add.addEventListener('click', () => {
            if (rows().length >= max) return;
            list.append(template.content.cloneNode(true));
            reindex();
            rows().at(-1).querySelector('select').focus();
        });
        list.addEventListener('click', event => {
            const button = event.target.closest('[data-remove-line]');
            if (!button || rows().length < 2) return;
            button.closest('[data-trade-line]').remove();
            reindex();
            add.focus();
        });
        form.addEventListener('input', update);
        form.addEventListener('change', update);
        form.addEventListener('submit', event => {
            if (form.dataset.submitted) { event.preventDefault(); return; }
            update();
            form.dataset.submitted = '1';
        });
        window.addEventListener('pageshow', () => { delete form.dataset.submitted; });
        reindex();
    });
})();
