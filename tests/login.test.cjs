const { test } = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const path = require('node:path');
const source = fs.readFileSync(path.join(__dirname, '../Panaderia.MVC/wwwroot/js/login.js'), 'utf8');

function setup() {
    const events = {};
    const button = { disabled: false };
    let reloads = 0;
    vm.runInNewContext(source, {
        document: { getElementById: () => ({
            addEventListener: (name, fn) => { events[name] = fn; },
            querySelector: () => button
        }) },
        window: {
            addEventListener: (name, fn) => { events[name] = fn; },
            location: { reload: () => { reloads++; } }
        }
    });
    return { events, button, get reloads() { return reloads; } };
}

test('only the first login submission is allowed', () => {
    const state = setup();
    let prevented = 0;
    const event = { preventDefault: () => { prevented++; } };
    state.events.submit(event);
    assert.equal(prevented, 0);
    assert.equal(state.button.disabled, true);
    state.events.submit(event);
    assert.equal(prevented, 1);
});

test('history restoration refreshes the login form; normal page load does not loop', () => {
    const state = setup();
    state.events.pageshow({ persisted: false });
    assert.equal(state.reloads, 0);
    state.events.pageshow({ persisted: true });
    assert.equal(state.reloads, 1);
});
