(() => {
    'use strict';
    const form = document.querySelector('form[data-pos]');
    if (!form || !window.AdghTrade) return;
    const { cents, display, decimalInput, quantity, searchKey, quickMatch, paymentPreview } = window.AdghTrade;
    const find = selector => form.querySelector(selector);
    const fallback = find('[data-pos-fallback]');
    const cart = find('[data-pos-cart]');
    const grid = find('[data-pos-grid]');
    const search = find('[data-pos-search]');
    const category = find('[data-pos-category]');
    const currency = find('[data-trade-currency]');
    const client = find('#ClientId');
    const clientName = find('#CustomerName');
    const options = find('[data-pos-options]');
    const discount = find('[data-trade-discount]');
    const method = find('#PaymentMethod');
    const deposit = find('#InitialPayment');
    const cash = find('[data-pos-cash]');
    const tendered = find('[data-pos-tendered]');
    const submit = find('[data-pos-submit]');
    const expected = find('[data-expected-total]');
    const maximum = cents(form.dataset.maximum);
    const maxLines = Number(form.dataset.maxLines);
    const pageSize = 24;
    const text = (selector, value, root = form) => {
        const node = root.querySelector(selector);
        if (node.textContent !== value) node.textContent = value;
    };
    const catalogue = [...fallback.querySelector('select[data-role="product"]').options]
        .filter(option => option.dataset.name && option.value)
        .map(option => {
            const { name, sku, category, stock, low, priceCdf, priceUsd } = option.dataset;
            const nameKey = searchKey(name);
            const skuKey = searchKey(sku);
            const categoryKey = searchKey(category);
            return { id: option.value, name, sku, category, nameKey, skuKey, categoryKey, stock: Number(stock),
                low: Number(low), prices: { CDF: cents(priceCdf), USD: cents(priceUsd) }, keywords: `${nameKey} ${skuKey} ${categoryKey}` };
        });
    const products = new Map(catalogue.map(product => [product.id, product]));
    const groups = new Map(catalogue.filter(product => product.categoryKey).map(product => [product.categoryKey, product.category]));
    const tones = new Map();
    [...groups].sort((a, b) => a[1].localeCompare(b[1], 'fr')).forEach(([key, name], index) => {
        category.add(new Option(name, key));
        tones.set(key, index % 4);
    });

    const lines = [];
    let quantities = new Map();
    let matches = [];
    let limit = pageSize;
    let currencyNotice = '';
    let displayed = [];
    let previousCurrency = currency.value;
    const mode = () => find('input[name="PaymentMode"]:checked')?.value;
    const articleCount = value => `${value} article${value > 1 ? 's' : ''}`;
    const remaining = product => Math.max(0, product.stock - (quantities.get(product.id) || 0));
    const getPrice = product => product?.prices[currency.value] ?? null;
    const announce = message => text('[data-pos-announcement]', message);
    const notice = find('[data-pos-catalog-message]');
    function catalogNotice(message) {
        notice.textContent = message;
        notice.hidden = !message;
    }

    function appendLine(id, rawQuantity) {
        const product = products.get(id);
        const name = product?.name || 'Produit indisponible';
        const node = find('[data-pos-line-template]').content.firstElementChild.cloneNode(true);
        const idInput = node.querySelector('[data-pos-product-id]');
        const input = node.querySelector('[data-pos-quantity]');
        const more = node.querySelector('[data-pos-adjust="1"]');
        const less = node.querySelector('[data-pos-adjust="-1"]');
        const remove = node.querySelector('[data-pos-remove]');
        idInput.value = id;
        input.value = rawQuantity;
        input.max = Math.min(product?.stock || 1, 100000);
        input.setAttribute('aria-label', `Quantité : ${name}`);
        more.setAttribute('aria-label', `Augmenter la quantité : ${name}`);
        less.setAttribute('aria-label', `Diminuer la quantité : ${name}`);
        remove.setAttribute('aria-label', `Retirer : ${name}`);
        text('[data-pos-name]', name, node);
        lines.push({ id, product, node, idInput, input, more, less, remove, warning: node.querySelector('[data-pos-line-error]') });
        cart.append(node);
    }

    function reindex() {
        lines.forEach((line, index) => {
            line.idInput.name = `Items[${index}].ProductId`;
            line.input.name = `Items[${index}].Quantity`;
            line.warning.id = `pos-warning-${index}`;
            line.input.setAttribute('aria-describedby', line.warning.id);
        });
    }

    function canAdd(product) {
        const line = lines.find(item => item.id === product.id);
        return getPrice(product) != null && remaining(product) > 0 && (line
            ? quantity(line.input.value) != null && quantity(line.input.value) < 100000
            : lines.length < maxLines);
    }

    function updateTiles() {
        for (const { product, tile } of displayed) {
            const left = remaining(product);
            const price = getPrice(product);
            const stock = tile.querySelector('[data-pos-stock]');
            stock.textContent = left ? `${left} en stock` : 'Stock au panier';
            stock.classList.toggle('is-low', left <= product.low);
            text('[data-pos-tile-price]', price == null ? 'Devise indisponible' : display(price, currency.value), tile);
            tile.disabled = !canAdd(product);
        }
    }

    function renderCatalog() {
        const key = searchKey(search.value);
        const words = key.split(' ').filter(Boolean);
        matches = catalogue.filter(product => (!category.value || category.value === product.categoryKey)
            && words.every(word => product.keywords.includes(word)));
        if (key) {
            const rank = product => product.skuKey === key ? 0 : product.nameKey === key ? 1 : 2;
            matches.sort((a, b) => rank(a) - rank(b));
        }
        const fragment = document.createDocumentFragment();
        displayed = matches.slice(0, limit).map(product => {
            const tile = find('[data-pos-tile-template]').content.firstElementChild.cloneNode(true);
            tile.dataset.posTile = product.id;
            tile.dataset.tone = tones.get(product.categoryKey) ?? 0;
            tile.setAttribute('aria-label', `Ajouter ${product.name}`);
            const descriptionIds = ['tile-ref', 'stock', 'tile-price'].map(field => {
                const id = `pos-${field}-${product.id}`;
                tile.querySelector(`[data-pos-${field}]`).id = id;
                return id;
            });
            tile.setAttribute('aria-describedby', descriptionIds.join(' '));
            text('[data-pos-tile-name]', product.name, tile);
            text('[data-pos-tile-ref]', [product.category, product.sku].filter(Boolean).join(' · ') || 'Article du catalogue', tile);
            fragment.append(tile);
            return { product, tile };
        });
        grid.replaceChildren(fragment);
        find('[data-pos-no-results]').hidden = matches.length > 0;
        find('[data-pos-more-area]').hidden = matches.length <= limit;
        text('[data-pos-result-count]', `${matches.length} référence${matches.length > 1 ? 's' : ''}`);
        updateTiles();
    }

    function setPaymentFields() {
        const selection = mode();
        find('[data-partial-payment]').hidden = selection !== 'Partial';
        deposit.disabled = selection !== 'Partial';
        find('[data-payment-method]').hidden = selection === 'Unpaid';
        method.disabled = selection === 'Unpaid';
        cash.hidden = selection !== 'Full' || method.value !== 'Espèces';
    }

    function update() {
        quantities = new Map();
        let count = 0;
        for (const line of lines) {
            const units = quantity(line.input.value) || 0;
            count += units;
            quantities.set(line.id, (quantities.get(line.id) || 0) + units);
        }
        let subtotal = 0n;
        let issue = '';
        for (const line of lines) {
            const product = line.product;
            const price = getPrice(product);
            const units = quantity(line.input.value);
            const warning = !product ? 'Ce produit n’est plus disponible. Retirez-le du panier.'
                : units == null ? 'Indiquez une quantité entre 1 et 100 000.'
                : quantities.get(line.id) > product.stock ? `Il reste ${product.stock} en stock pour ce produit.`
                : price == null ? 'Ce produit ne peut pas être vendu dans cette devise.' : '';
            line.warning.hidden = !warning;
            line.warning.textContent = warning;
            line.input.setAttribute('aria-invalid', String(!!warning));
            line.less.disabled = units == null || units <= 1;
            line.more.disabled = !product || !canAdd(product) || units == null || units >= 100000;
            text('[data-pos-unit]', price == null ? '' : `${display(price, currency.value)} / unité`, line.node);
            const lineTotal = price != null && units != null ? price * BigInt(units) : null;
            text('[data-pos-line-total]', lineTotal == null ? '—' : display(lineTotal, currency.value), line.node);
            if (warning) issue ||= warning;
            else subtotal += lineTotal;
        }
        const enteredDiscount = cents(discount?.value.trim() || '0');
        if (lines.length && !issue) {
            if (subtotal > maximum) issue = 'Le montant dépasse la limite d’une vente.';
            else if (enteredDiscount == null || enteredDiscount > subtotal) issue = 'La remise doit être un montant valide inférieur ou égal au sous-total.';
        }
        const total = issue ? null : subtotal - (lines.length ? enteredDiscount : 0n);
        expected.value = lines.length && total != null ? decimalInput(total) : '';
        const formattedTotal = total == null ? '—' : display(total, currency.value);
        text('[data-pos-count]', articleCount(count));
        text('[data-pos-mobile-count]', articleCount(count));
        text('[data-trade-total]', formattedTotal);
        text('[data-pos-mobile-total]', formattedTotal);
        find('[data-pos-cart-empty]').hidden = lines.length > 0;
        find('[data-pos-adjustments]').hidden = !lines.length || enteredDiscount == null || enteredDiscount === 0n;
        text('[data-pos-subtotal]', total == null ? '—' : display(subtotal, currency.value));
        text('[data-pos-discount-value]', total == null ? '—' : `−${display(enteredDiscount ?? 0n, currency.value)}`);
        text('[data-pos-client-label]', (client?.value && client.selectedOptions[0]?.textContent) || clientName.value.trim() || 'Client de passage');
        setPaymentFields();
        const selected = mode();
        const payment = lines.length && total != null ? paymentPreview(total, selected, deposit.value,
            cash.hidden || !cash.open ? '' : tendered.value) : { balance: null, change: null, error: null };
        const requiresClient = payment.balance != null && payment.balance > 0n;
        issue ||= payment.error || '';
        if (requiresClient && !client?.value && !clientName.value.trim()) issue ||= 'Indiquez un client pour suivre le reste à régler.';
        find('[data-pos-balance-row]').hidden = !lines.length || selected === 'Full' || payment.balance == null;
        text('[data-pos-balance]', payment.balance == null ? '—' : display(payment.balance, currency.value));
        text('[data-pos-change]', payment.change == null ? '—' : display(payment.change, currency.value));
        const message = issue || currencyNotice;
        const notice = find('[data-pos-message]');
        notice.hidden = !message;
        notice.classList.toggle('is-error', !!issue);
        text('[data-pos-message]', message);
        const ready = lines.length > 0 && !issue;
        submit.disabled = !ready || !!form.dataset.submitted;
        text('[data-pos-submit-label]', form.dataset.submitted ? 'Enregistrement…'
            : selected === 'Unpaid' ? 'Enregistrer à crédit' : selected === 'Partial' ? 'Enregistrer l’acompte' : 'Encaisser');
        updateTiles();
        return ready;
    }

    function addProduct(product) {
        if (!canAdd(product)) {
            catalogNotice('Impossible d’ajouter cet article : vérifiez le stock, la quantité ou la devise.');
            return false;
        }
        const line = lines.find(item => item.id === product.id);
        if (line) line.input.value = quantity(line.input.value) + 1;
        else { appendLine(product.id, '1'); reindex(); }
        catalogNotice('');
        update();
        announce(`${product.name} ajouté. ${articleCount(quantities.get(product.id))} de ce produit au panier.`);
        return true;
    }

    const newSearch = () => { limit = pageSize; catalogNotice(''); renderCatalog(); };
    search.addEventListener('input', newSearch);
    category.addEventListener('change', newSearch);
    find('[data-pos-more]').addEventListener('click', () => {
        const firstNew = limit;
        limit += pageSize;
        renderCatalog();
        displayed[firstNew]?.tile.focus();
    });
    grid.addEventListener('click', event => {
        const tile = event.target.closest('[data-pos-tile]');
        if (!tile || !addProduct(products.get(tile.dataset.posTile))) return;
        if (event.detail === 0 && tile.disabled) {
            const following = displayed.slice(displayed.findIndex(item => item.tile === tile) + 1);
            (following.find(item => !item.tile.disabled)?.tile || search).focus();
        }
    });
    search.addEventListener('keydown', event => {
        if (event.isComposing) return;
        if (event.key === 'Escape' && search.value) { search.value = ''; newSearch(); }
        if (event.key === 'ArrowDown') {
            const first = grid.querySelector('button:not(:disabled)');
            if (first) { event.preventDefault(); first.focus(); }
        }
        if (event.key !== 'Enter') return;
        event.preventDefault();
        const product = quickMatch(matches, search.value);
        if (product && addProduct(product)) { search.value = ''; newSearch(); }
        else if (!product && search.value.trim()) catalogNotice(matches.length ? 'Plusieurs articles correspondent. Choisissez un article.' : 'Aucun article ne correspond à cette recherche.');
    });
    cart.addEventListener('click', event => {
        const button = event.target.closest('button');
        const index = button && lines.findIndex(line => line.node.contains(button));
        if (index == null || index < 0) return;
        const line = lines[index];
        if (button === line.remove) {
            line.node.remove();
            lines.splice(index, 1);
            reindex();
            update();
            (lines[index] || lines.at(-1))?.remove.focus();
            if (!lines.length) find('#pos-ticket-title').focus({ preventScroll: true });
            announce(`${line.product?.name || 'Article'} retiré du panier.`);
        } else if (button === line.more || button === line.less) {
            const next = quantity(line.input.value) + Number(button.dataset.posAdjust);
            if (next < 1 || next > 100000 || button.disabled) return;
            line.input.value = next;
            update();
            if (button.disabled) line.input.focus({ preventScroll: true });
        }
    });

    function showCatalog(focusSearch) {
        form.classList.remove('is-ticket-view');
        const destination = focusSearch ? search : find('#pos-catalog-title');
        destination.focus({ preventScroll: true });
        destination.scrollIntoView({ block: 'nearest' });
        if (focusSearch) search.select();
    }
    find('[data-pos-back]').addEventListener('click', event => showCatalog(event.detail === 0));
    find('[data-pos-open]').addEventListener('click', () => {
        form.classList.add('is-ticket-view');
        find('#pos-ticket-title').focus({ preventScroll: true });
        find('#pos-ticket').scrollIntoView({ block: 'start' });
    });
    document.addEventListener('keydown', event => {
        if (event.key !== 'F2' || document.body.classList.contains('sidebar-open') || document.querySelector('.modal.show')) return;
        event.preventDefault();
        showCatalog(true);
    });
    form.addEventListener('keydown', event => {
        // Require explicit activation of the sale button; Enter in a field never records a sale.
        if (event.key === 'Enter' && !event.isComposing && event.target instanceof HTMLInputElement) event.preventDefault();
    });
    form.addEventListener('input', event => {
        if (event.target === search || event.target === category || event.target === currency) return;
        if ([discount, deposit, tendered].includes(event.target)) currencyNotice = '';
        update();
    });
    form.addEventListener('change', event => {
        if (event.target === search || event.target === category) return;
        if (event.target === currency && previousCurrency !== currency.value) {
            const changedAmounts = (discount && discount.value !== '' && cents(discount.value) !== 0n) || deposit.value || tendered.value;
            if (discount) discount.value = '0';
            deposit.value = tendered.value = '';
            currencyNotice = changedAmounts ? 'Devise modifiée : les montants saisis ont été réinitialisés.' : '';
            previousCurrency = currency.value;
        }
        if (event.target.matches('input[name="PaymentMode"]') && mode() !== 'Full' && !client?.value && !clientName.value.trim()) options.open = true;
        update();
    });
    cash.addEventListener('toggle', update);
    form.addEventListener('submit', event => {
        if (form.dataset.submitted || !update()) { event.preventDefault(); return; }
        if (discount && !discount.value.trim()) discount.value = '0';
        form.dataset.submitted = '1';
        submit.setAttribute('aria-busy', 'true');
        update();
    });
    window.addEventListener('pageshow', event => {
        if (!event.persisted) return;
        delete form.dataset.submitted;
        submit.removeAttribute('aria-busy');
        update();
    });

    for (const row of fallback.querySelectorAll('[data-trade-line]')) {
        const id = row.querySelector('[data-role="product"]').value;
        if (id) appendLine(id, row.querySelector('[data-role="quantity"]').value);
    }
    reindex();
    const hasErrors = !!document.querySelector('[data-pos-server-errors].validation-summary-errors');
    options.open = hasErrors || !!client?.value || !!clientName.value.trim() || (discount != null && cents(discount.value) !== 0n) || mode() !== 'Full';
    if (hasErrors) form.classList.add('is-ticket-view');
    fallback.querySelectorAll('input, select').forEach(control => { control.disabled = true; });
    fallback.hidden = true;
    find('[data-pos-catalog]').hidden = false;
    find('[data-pos-basket]').hidden = false;
    form.classList.add('is-pos-ready');
    renderCatalog();
    update();
})();
