const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const view = fs.readFileSync('Panaderia.MVC/Views/Pedido/PlanificarAmasadas.cshtml', 'utf8');
const source = view.match(/<script>([\s\S]*?)<\/script>/)[1]
  .replace('@Html.Raw(ViewBag.ProductosJson)', '[]')
  .replace('@Html.Raw(ViewBag.SubRecetasJson)', JSON.stringify([
    { idSubReceta: 1, nombre: 'Masa madre', totalGramos: 1000, ingredientes: [{ nombreInsumo: 'Harina', cantidad: 500, unidad: 'g' }] },
    { idSubReceta: 2, nombre: 'Soaker', totalGramos: 200, ingredientes: [{ nombreInsumo: 'Semillas', cantidad: 100, unidad: 'g' }] }
  ]));
function imprimir(query) {
  const area = {};
  const context = vm.createContext({ URLSearchParams, location: { search: query }, setTimeout() {},
    document: { addEventListener() {}, getElementById() { return area; }, createElement() { return { set textContent(v) { this.innerHTML = v; } }; } } });
  vm.runInContext(source + '\nimprimirSinConfirmar();', context);
  return area.innerHTML;
}
test('imprime preparaciones con la cantidad anterior descontada', () => {
  const html = imprimir('?anteriores=1:200,2:50');
  assert.match(html, /A preparar: <strong>800 g/);
  assert.match(html, /400 g<\/td>/);
  assert.match(html, /75 g<\/td>/);
});
test('sin anterior conserva cantidades; exceso nunca da negativos', () => {
  assert.match(imprimir(''), /500 g<\/td>/);
  const html = imprimir('?anteriores=1:1500,2:Infinity');
  assert.match(html, /A preparar: <strong>0 g/);
  assert.match(html, /100 g<\/td>/);
  assert.doesNotMatch(html, /NaN|Infinity|-250/);
});

function flujo(response) {
  const state = { requests: 0, prints: 0, shown: 0, hidden: 0, errors: [] };
  const buttons = [{ disabled: false }, { disabled: false }];
  const elements = {
    confirmarPlanificacionForm: { action: '/Pedido/ConfirmarPlanificacion' },
    confirmarImpresionModal: {}, produccionConfirmada: { style: { display: 'none' } }, 'print-area': {}
  };
  const context = vm.createContext({ URLSearchParams, location: { search: '' },
    setTimeout(fn) { fn(); }, window: { print() { state.prints++; } },
    alert(message) { state.errors.push(message); }, FormData: class { constructor(form) { this.form = form; } },
    async fetch(url, options) {
      state.requests++;
      assert.equal(url, '/Pedido/ConfirmarPlanificacion');
      assert.equal(options.method, 'POST');
      assert.equal(options.body.form, elements.confirmarPlanificacionForm);
      return response();
    },
    bootstrap: { Modal: { getOrCreateInstance() { return { show() { state.shown++; }, hide() { state.hidden++; } }; } } },
    document: {
      addEventListener() {}, querySelectorAll() { return buttons; }, getElementById(id) { return elements[id]; },
      createElement() { return { set textContent(v) { this.innerHTML = v; } }; }
    }
  });
  vm.runInContext(source.replace('const productosOriginales = [];', 'const productosOriginales = [{ idProducto: 1 }];'), context);
  return { state, buttons, elements, run(code) { return vm.runInContext(code, context); } };
}
const ok = () => ({ ok: true, redirected: false, json: async () => ({ success: true }) });

test('al imprimir pregunta; solo imprimir no confirma ni hace solicitudes', () => {
  const f = flujo(ok);
  f.run('solicitarImpresion()');
  assert.equal(f.state.shown, 1);
  assert.equal(f.state.prints, 0);
  assert.equal(f.state.requests, 0);
  f.run('soloImprimir()');
  assert.equal(f.state.prints, 1);
  assert.equal(f.state.requests, 0);
});

test('confirmar e imprimir envía una vez; reimprimir nunca repite la confirmación', async () => {
  const f = flujo(ok);
  await f.run('confirmarEImprimir()');
  assert.equal(f.state.requests, 1);
  assert.equal(f.state.prints, 1);
  assert.equal(f.elements.produccionConfirmada.style.display, '');
  f.run('solicitarImpresion()');
  await f.run('confirmarEImprimir()');
  assert.equal(f.state.requests, 1);
  assert.equal(f.state.prints, 3);
  assert.equal(f.state.shown, 0);
});

test('doble clic y solo imprimir quedan bloqueados mientras se confirma', async () => {
  let resolve;
  const f = flujo(() => new Promise(r => { resolve = r; }));
  const first = f.run('confirmarEImprimir()');
  await f.run('confirmarEImprimir()');
  f.run('soloImprimir(); solicitarImpresion()');
  assert.equal(f.state.requests, 1);
  assert.equal(f.state.prints, 0);
  assert.ok(f.buttons.every(b => b.disabled));
  resolve(ok());
  await first;
  assert.equal(f.state.prints, 1);
  assert.ok(f.buttons.every(b => !b.disabled));
});

test('un rechazo o error de red no imprime ni deja la interfaz bloqueada', async () => {
  for (const response of [
    () => ({ ok: true, json: async () => ({ success: false, error: 'Plan desactualizado' }) }),
    () => ({ ok: false }),
    () => ({ ok: true, redirected: true }),
    () => { throw new Error('Sin conexión'); }
  ]) {
    const f = flujo(response);
    await f.run('confirmarEImprimir()');
    assert.equal(f.state.prints, 0);
    assert.equal(f.state.errors.length, 1);
    assert.equal(f.elements.produccionConfirmada.style.display, 'none');
    assert.ok(f.buttons.every(b => !b.disabled));
    f.run('solicitarImpresion()');
    assert.equal(f.state.shown, 1);
  }
});
