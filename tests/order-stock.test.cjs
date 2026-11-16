const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const view = fs.readFileSync('Panaderia.MVC/Views/Pedido/Edit.cshtml', 'utf8');
const limits = view.slice(view.indexOf('function esEncargoOriginal('), view.indexOf('function clienteACosto()'));

function calculate(stock, quantities, reserved = 0, madeToOrder = false, originalMadeToOrder = false) {
  const rows = quantities.map((quantity, i) => {
    const input = { value: String(quantity), removeAttribute(key) { delete this[key]; } };
    const text = {};
    const select = { value: '3', selectedIndex: 0, options: [{ value: '3', dataset: { stock: String(stock), porEncargo: String(madeToOrder) } }] };
    return { input, text, querySelector(selector) { return { '.prod-select': select, '.cant-input': input, '.stock-disponible': text, '.detalle-id': { value: String(i + 1) } }[selector]; } };
  });
  vm.runInNewContext(limits + '; actualizarLimitesStock();', {
    tbody: { querySelectorAll() { return rows; } }, reservasIniciales: { 3: reserved }, encargosOriginales: originalMadeToOrder ? { 1: 3 } : {}
  });
  return rows;
}

test('una línea por encargo anterior no inventa stock reservado', () => {
  const [row] = calculate(1, [1]);
  assert.equal(row.input.max, 1);
  assert.equal(row.input.value, '1');
});

test('un encargo original conserva su modo aunque ahora el producto requiera stock', () => {
  const [row] = calculate(0, [3], 0, false, true);
  assert.equal(row.input.max, undefined);
  assert.equal(row.input.value, '3');
});

test('al editar se puede conservar la última unidad ya reservada', () => {
  const [row] = calculate(0, [1], 1);
  assert.equal(row.input.max, 1);
  assert.equal(row.input.value, '1');
});

test('stock insuficiente marca el límite sin borrar ni reducir cantidades cargadas', () => {
  const [row] = calculate(0, [2]);
  assert.equal(row.input.max, 0);
  assert.equal(row.input.value, '2');
});

test('varias líneas comparten el stock y los productos por encargo no tienen límite', () => {
  const rows = calculate(2, [1, 2]);
  assert.deepEqual(rows.map(r => r.input.max), [0, 1]);
  assert.deepEqual(rows.map(r => r.input.value), ['1', '2']);
  assert.equal(calculate(0, [5], 0, true)[0].input.max, undefined);
});
