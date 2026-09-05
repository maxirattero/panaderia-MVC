const { test } = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const path = require('node:path');
const source = fs.readFileSync(path.join(__dirname, '../Panaderia.MVC/wwwroot/js/order-notifications.js'), 'utf8');
const flush = () => new Promise(resolve => setImmediate(resolve));

async function setup({ stored = 0, failure = false, denied = false } = {}) {
    const elements = new Map();
    function element() {
        return { dataset: {}, events: {}, children: [], hidden: false,
            addEventListener(name, fn) { this.events[name] = fn; },
            setAttribute(name, value) { this[name] = value; },
            replaceChildren() { this.children = []; },
            append(...children) { this.children.push(...children); }
        };
    }
    for (const id of ['notificacionesPedidos', 'btnPedidosNuevos', 'contadorPedidosNuevos', 'listaPedidosNuevos', 'estadoPedidosNuevos', 'marcarPedidosVistos']) elements.set(id, element());
    elements.get('notificacionesPedidos').dataset = { userId: 'admin-1', feedUrl: '/feed', detailUrl: '/Pedido/Details' };
    const window = element();
    const document = Object.assign(element(), { getElementById: id => elements.get(id), createElement: element });
    let latest = 2;
    let calls = 0;
    let interval;
    let saved;
    vm.runInNewContext(source, {
        window, document, Intl, AbortSignal,
        localStorage: { getItem: () => stored, setItem: (key, value) => { saved = { key, value }; } },
        setInterval: fn => { interval = fn; },
        fetch: async url => {
            calls++;
            if (failure) throw new TypeError('offline');
            const seen = Number(new URL(url, 'http://localhost').searchParams.get('ultimoVisto'));
            return { ok: !denied, status: denied ? 403 : 200,
                json: async () => ({ ultimoId: latest, nuevos: Math.max(0, latest - seen),
                    pedidos: [{ id: latest, cliente: '<img src=x onerror=alert(1)>', montoTotal: 2000, fechaCreacion: '2026-09-05T12:00:00Z' }] }) };
        }
    });
    await flush();
    return { elements, window, document, interval, get saved() { return saved; }, get calls() { return calls; }, add() { latest++; } };
}

test('shows unread badge and safe customer text without notification APIs', async () => {
    const state = await setup();
    assert.equal(state.elements.get('contadorPedidosNuevos').textContent, '2');
    assert.equal(state.elements.get('contadorPedidosNuevos').hidden, false);
    const row = state.elements.get('listaPedidosNuevos').children[0];
    assert.equal(row.href, '/Pedido/Details?id=2');
    assert.match(row.children[0].textContent, /<img/);
    assert.equal(row.children[0].innerHTML, undefined);
});

test('mark seen persists per user and next poll detects a new order', async () => {
    const state = await setup();
    state.elements.get('marcarPedidosVistos').events.click();
    assert.deepEqual(state.saved, { key: 'mv_pedidos_vistos_admin-1', value: '2' });
    assert.equal(state.elements.get('contadorPedidosNuevos').hidden, true);
    state.add();
    await state.interval();
    await flush();
    assert.equal(state.elements.get('contadorPedidosNuevos').textContent, '1');
    assert.equal(state.elements.get('contadorPedidosNuevos').hidden, false);
});

test('read state survives reload and background tabs skip polling', async () => {
    const state = await setup({ stored: 2 });
    assert.equal(state.elements.get('contadorPedidosNuevos').hidden, true);
    state.document.hidden = true;
    state.interval();
    assert.equal(state.calls, 1);
});

test('connection error is visible and retries on next poll', async () => {
    const state = await setup({ failure: true });
    assert.match(state.elements.get('estadoPedidosNuevos').textContent, /Sin conexión/);
    state.interval();
    await flush();
    assert.equal(state.calls, 2);
});

test('unauthorized session stops polling and explains how to recover', async () => {
    const state = await setup({ denied: true });
    assert.match(state.elements.get('estadoPedidosNuevos').textContent, /Iniciá sesión/);
    state.interval();
    assert.equal(state.calls, 1);
});
