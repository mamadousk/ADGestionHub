// Money used in the sale preview stays in integer minor units. The server recomputes it on save.
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
    const fraction = amount => String(amount % 100n).padStart(2, '0');
    const display = (amount, currency) => `${french.format(amount / 100n)},${fraction(amount)} ${currency}`;
    const decimalInput = amount => `${amount / 100n}.${fraction(amount)}`;
    const quantity = value => /^\d{1,6}$/.test(value) && Number(value) >= 1 && Number(value) <= 100000 ? Number(value) : null;
    const searchKey = value => String(value ?? '').normalize('NFD').replace(/\p{M}/gu, '').toLowerCase().trim().replace(/\s+/g, ' ');

    function quickMatch(results, query) {
        const key = searchKey(query);
        if (!key) return null;
        const exact = results.filter(product => product.skuKey === key);
        if (exact.length) return exact.length === 1 ? exact[0] : null;
        return results.length === 1 ? results[0] : null;
    }

    function paymentPreview(total, mode, deposit, tendered) {
        if (mode === 'Unpaid') return { balance: total, change: null, error: null };
        if (mode === 'Partial') {
            const amount = cents(deposit);
            return amount != null && amount > 0n && amount < total
                ? { balance: total - amount, change: null, error: null }
                : { balance: null, change: null, error: 'Indiquez un acompte supérieur à zéro et inférieur au total.' };
        }
        if (mode !== 'Full') return { balance: null, change: null, error: 'Choisissez le type de règlement.' };
        if (!tendered?.trim()) return { balance: 0n, change: null, error: null };
        const received = cents(tendered);
        return received != null && received >= total
            ? { balance: 0n, change: received - total, error: null }
            : { balance: 0n, change: null, error: received == null
                ? 'Vérifiez la somme remise (deux décimales au maximum).'
                : 'La somme remise ne couvre pas le total.' };
    }

    const trade = { cents, display, decimalInput, quantity, searchKey, quickMatch, paymentPreview };
    if (typeof module !== 'undefined' && module.exports) module.exports = trade;
    if (typeof window !== 'undefined') window.AdghTrade = trade;
})();
