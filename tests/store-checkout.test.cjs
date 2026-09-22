const { test } = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const path = require('node:path');
const root = path.join(__dirname, '../Panaderia.MVC');
const copySource = fs.readFileSync(path.join(root, 'wwwroot/js/alias-transferencia.js'), 'utf8');

function copySetup(clipboard, fallbackWorks = true) {
    let click;
    let fallbackValue;
    let removed = false;
    const status = { textContent: '' };
    const button = { addEventListener: (_, handler) => { click = handler; }, focus() {} };
    const field = { style: {}, select() {}, setSelectionRange() {}, remove() { removed = true; } };
    const container = { querySelector: selector => ({
        '.alias-copiar': button, '.alias-estado': status, '[data-alias]': { textContent: 'masa.viva.pan' }
    })[selector] };
    vm.runInNewContext(copySource, {
        navigator: { clipboard },
        document: {
            querySelectorAll: () => [container], createElement: () => field,
            body: { appendChild() {} },
            execCommand: () => { fallbackValue = field.value; return fallbackWorks; }
        }
    });
    return { click, status, get fallbackValue() { return fallbackValue; }, get removed() { return removed; } };
}

test('copies only the exact alias and confirms success', async () => {
    let copied;
    const state = copySetup({ writeText: async value => { copied = value; } });
    await state.click();
    assert.equal(copied, 'masa.viva.pan');
    assert.equal(state.status.textContent, '¡Alias copiado!');
});

test('unavailable or denied clipboard uses fallback and cleans up', async () => {
    for (const clipboard of [undefined, { writeText: async () => { throw new Error('Denied'); } }]) {
        const state = copySetup(clipboard);
        await state.click();
        assert.equal(state.fallbackValue, 'masa.viva.pan');
        assert.equal(state.status.textContent, '¡Alias copiado!');
        assert.equal(state.removed, true);
    }
});

test('failed copy gives manual instructions without claiming success', async () => {
    const state = copySetup(undefined, false);
    await state.click();
    assert.match(state.status.textContent, /No se pudo copiar/);
    assert.equal(state.removed, true);
});

test('pickup forces transfer on initial load and delivery re-enables cash', () => {
    const checkout = fs.readFileSync(path.join(root, 'Views/Tienda/Checkout.cshtml'), 'utf8');
    const source = checkout.match(/<script>\s*([\s\S]*?)<\/script>/)[1];
    let delivery = 'retiro';
    const cash = { disabled: false, checked: true };
    const transfer = { value: 'transferencia', checked: false };
    const elements = { campoDireccion: { style: {} }, aliasAviso: { style: {} }, avisoPagoRetiro: {} };
    const context = vm.createContext({ document: {
        getElementById: id => elements[id],
        querySelector: selector => {
            if (selector === 'input[name="Entrega"]:checked') return { value: delivery };
            if (selector === 'input[name="MedioPago"]:checked') return transfer.checked ? transfer : { value: 'efectivo' };
            return selector.includes('efectivo') ? cash : transfer;
        }
    } });
    vm.runInContext(source, context);
    assert.equal(cash.disabled, true);
    assert.equal(transfer.checked, true);
    assert.equal(elements.aliasAviso.style.display, '');
    assert.equal(elements.avisoPagoRetiro.hidden, false);
    delivery = 'delivery';
    context.toggleDireccion();
    assert.equal(cash.disabled, false);
    assert.equal(elements.campoDireccion.style.display, '');
    assert.equal(elements.avisoPagoRetiro.hidden, true);
    transfer.checked = false;
    context.toggleAlias();
    assert.equal(elements.aliasAviso.style.display, 'none');
    delivery = 'retiro';
    context.toggleDireccion();
    assert.equal(cash.disabled, true);
    assert.equal(transfer.checked, true);
    assert.equal(elements.aliasAviso.style.display, '');
});
