const test = require('node:test');
const { strictEqual } = require('node:assert');
const { cents, display } = require('../wwwroot/js/trade');

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
