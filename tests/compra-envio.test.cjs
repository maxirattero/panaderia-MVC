const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '../Panaderia.MVC/Views/Compra/Create.cshtml'), 'utf8');
const start = source.indexOf('    function distribuirEnvio()');
const end = source.indexOf('    function actualizarCheckboxesEnvio()', start);

function distribuir(cantidades, envio, seleccionados = cantidades.map((_, i) => i), precios = cantidades.map(() => 100)) {
    const rows = cantidades.map((cantidad, i) => {
        const elements = {
            '.inp-cantidad': { value: String(cantidad) },
            '.inp-precio': { value: String(precios[i]) },
            '.inp-costo-envio': { value: '0' },
            '.span-subtotal': { dataset: {} }
        };
        return { querySelector: selector => elements[selector], elements };
    });
    const context = {
        tbody: { querySelectorAll: () => rows },
        costoEnvioInput: { value: String(envio) },
        checkboxesDiv: { querySelectorAll: () => seleccionados.map(i => ({ dataset: { rowIndex: String(i) } })) },
        cultura: 'es-AR', actualizarTotal() {}
    };
    vm.runInNewContext(source.slice(start, end) + '\ndistribuirEnvio();', context);
    return {
        envios: rows.map(r => Number(r.elements['.inp-costo-envio'].value)),
        subtotales: rows.map(r => Number(r.elements['.span-subtotal'].dataset.value))
    };
}

test('compra de cinco bolsas en cuatro renglones: $6000 de envío por bolsa', () => {
    const result = distribuir([2, 1, 1, 1], 30000, undefined, [40787.12, 36687.09, 53085.42, 62955.27]);
    assert.deepEqual(result.envios, [12000, 6000, 6000, 6000]);
    assert.deepEqual(result.subtotales.map(x => Math.round(x * 100)), [9357424, 4268709, 5908542, 6895527]);
    assert.equal(Math.round(result.subtotales.reduce((a, b) => a + b, 0) * 100), 26430202);
});

test('agrupar o separar bolsas no cambia el envío unitario', () => {
    assert.deepEqual(distribuir([5], 30000).envios, [30000]);
    assert.deepEqual(distribuir([1, 1, 1, 1, 1], 30000).envios, [6000, 6000, 6000, 6000, 6000]);
});

test('los ítems desmarcados no reciben envío ni cuentan en el reparto', () => {
    assert.deepEqual(distribuir([2, 100, 1], 30000, [0, 2]).envios, [20000, 0, 10000]);
});

test('el redondeo conserva todos los centavos del envío', () => {
    const result = distribuir([1, 1, 1], 100);
    assert.equal(result.envios.reduce((total, value) => total + Math.round(value * 100), 0), 10000);
    assert.deepEqual(distribuir([1, 1, 1], 0.01).envios, [0, 0.01, 0]);
});

test('sin envío o sin cantidades válidas no se generan costos inválidos', () => {
    assert.deepEqual(distribuir([2, 1], 0).envios, [0, 0]);
    assert.deepEqual(distribuir([0, 0], 30000).envios, [0, 0]);
    assert.deepEqual(distribuir([2, 1], 30000, []).envios, [0, 0]);
});
