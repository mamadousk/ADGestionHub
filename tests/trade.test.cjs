const test = require('node:test');
const { strictEqual, deepStrictEqual } = require('node:assert');
const { cents, display, decimalInput, quantity, searchKey, quickMatch, paymentPreview } = require('../wwwroot/js/trade');

test('Saisie française et décimales conservées en centimes', () => {
    strictEqual(cents('1\u202f000,50'), 100050n);
    strictEqual(cents('2.35'), 235n);
    strictEqual(cents('4,5'), 450n);
    strictEqual(cents('4,5000'), 450n);
    strictEqual(cents('0.1') + cents('0.2'), 30n);
    strictEqual(display(30n, 'USD'), '0,30 USD');
});

test('Les séparateurs ambigus et valeurs invalides ne sont pas estimés', () => {
    for (const amount of ['', '0.001', '12,34.56', '3e4', '-10', '10,', '12 $,50', '1 2', '1,2 3', '1 23,45'])
        strictEqual(cents(amount), null, amount);
});

test('Un montant important ne perd pas de précision lors du calcul', () => {
    strictEqual(cents('100000000000.01') * 1000n, 10000000000001000n);
});

test('Ajout clavier : priorité à la référence exacte, sans choisir en cas d’ambiguïté', () => {
    const products = [
        { skuKey: 'a12', nameKey: searchKey('Crème de lait') },
        { skuKey: 'a123', nameKey: searchKey('Savon ménager') },
    ];
    strictEqual(quickMatch(products, '  A12 '), products[0]);
    strictEqual(searchKey('  Crème  de LAIT '), 'creme de lait');
    strictEqual(quickMatch(products, 'a1'), null);
    strictEqual(quickMatch([products[0], { skuKey: 'a123', nameKey: 'creme de lait bio' }], 'crème de lait'), null);
    strictEqual(quickMatch([products[0], { ...products[1], skuKey: 'a12' }], 'A12'), null);
    strictEqual(quickMatch([products[1]], 'ménager'), products[1]);
    strictEqual(quickMatch([products[0]], '   '), null);
});

test('Seules les quantités positives admises par le serveur sont retenues', () => {
    strictEqual(quantity('1'), 1);
    strictEqual(quantity('100000'), 100000);
    for (const value of ['', '0', '-1', '1.5', '1e2', '100001', '9999999']) strictEqual(quantity(value), null, value);
    strictEqual(decimalInput(499975n), '4999.75');
});

test('Acompte, crédit et monnaie calculés séparément du total à enregistrer', () => {
    deepStrictEqual(paymentPreview(499975n, 'Full', '', '5 000,00'), { balance: 0n, change: 25n, error: null });
    deepStrictEqual(paymentPreview(499975n, 'Partial', '1500,00'), { balance: 349975n, change: null, error: null });
    deepStrictEqual(paymentPreview(499975n, 'Unpaid'), { balance: 499975n, change: null, error: null });
    strictEqual(paymentPreview(499975n, 'Full', '', '').error, null);
    for (const amount of ['0', '4999,75', '5000', '9,999', '']) strictEqual(typeof paymentPreview(499975n, 'Partial', amount).error, 'string');
    for (const amount of ['4000', '1.234', '-5']) strictEqual(typeof paymentPreview(499975n, 'Full', '', amount).error, 'string');
});
