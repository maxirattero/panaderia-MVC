const { test } = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const path = require('node:path');
const source = fs.readFileSync(path.join(__dirname, '../Panaderia.MVC/wwwroot/js/push-notifications.js'), 'utf8');

async function setup({ permission = 'granted', existing = true, stale = false, saveFails = false, workerMissing = false } = {}) {
    const calls = { saved: 0, subscribed: 0, unsubscribed: 0, shown: 0, alerts: [] };
    let click;
    let active = false;
    const subscription = {
        options: { applicationServerKey: Uint8Array.from(stale ? [4, 5, 6] : [1, 2, 3]).buffer },
        toJSON: () => ({ endpoint: 'https://push.example/test', keys: {} }),
        unsubscribe: async () => { calls.unsubscribed++; }
    };
    const button = {
        dataset: { vapidPublicKey: 'AQID', antiforgeryToken: 'csrf' },
        classList: { add() {}, toggle: (_, value) => { active = value; } },
        setAttribute() {}, addEventListener: (_, handler) => { click = handler; }
    };
    const registration = {
        pushManager: {
            getSubscription: async () => existing ? subscription : null,
            subscribe: async () => { calls.subscribed++; return subscription; }
        },
        showNotification: async () => { calls.shown++; }
    };
    vm.runInNewContext(source, {
        document: { getElementById: () => button },
        navigator: { serviceWorker: { ready: workerMissing ? new Promise(() => {}) : Promise.resolve(registration) } },
        window: { PushManager: {}, Notification: {} },
        Notification: { permission, requestPermission: async () => 'granted' },
        fetch: async () => { calls.saved++; return { ok: !saveFails, status: saveFails ? 500 : 200 }; },
        AbortSignal, Uint8Array, atob, console: { error() {} },
        alert: message => calls.alerts.push(message),
        setTimeout: workerMissing ? callback => setTimeout(callback, 1) : setTimeout, clearTimeout
    });
    await new Promise(resolve => setTimeout(resolve, workerMissing ? 15 : 0));
    return { calls, button, click, isActive: () => active };
}

test('restores an existing subscription on the server without prompting or showing a notification', async () => {
    const state = await setup();
    assert.equal(state.calls.saved, 1);
    assert.equal(state.calls.subscribed, 0);
    assert.equal(state.calls.shown, 0);
    assert.equal(state.isActive(), true);
});

test('bell creates a missing subscription and displays a local test after saving', async () => {
    const state = await setup({ existing: false });
    await state.click();
    assert.equal(state.calls.subscribed, 1);
    assert.equal(state.calls.saved, 1);
    assert.equal(state.calls.shown, 1);
    assert.equal(state.isActive(), true);
    assert.equal(state.button.disabled, false);
});

test('blocked permission provides recovery instructions without subscribing', async () => {
    const state = await setup({ permission: 'denied' });
    await state.click();
    assert.match(state.calls.alerts[0], /Configuración del sitio/);
    assert.equal(state.calls.saved, 0);
});

test('old server key is repaired on click and never shown as active on load', async () => {
    const state = await setup({ stale: true });
    assert.equal(state.calls.saved, 0);
    assert.equal(state.isActive(), false);
    await state.click();
    assert.equal(state.calls.unsubscribed, 1);
    assert.equal(state.calls.subscribed, 1);
    assert.equal(state.calls.saved, 1);
});

test('server failure does not claim activation or show a success notification', async () => {
    const state = await setup({ saveFails: true });
    await state.click();
    assert.equal(state.isActive(), false);
    assert.equal(state.calls.shown, 0);
    assert.match(state.calls.alerts[0], /No se pudo guardar/);
    assert.equal(state.button.disabled, false);
});

test('missing service worker times out and leaves the bell available to retry', async () => {
    const state = await setup({ workerMissing: true });
    assert.equal(state.button.disabled, false);
    await state.click();
    assert.match(state.calls.alerts[0], /no inició/);
    assert.equal(state.button.disabled, false);
});
