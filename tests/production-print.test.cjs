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
  const area = { dataset: {}, classList: { contains() { return false; } } };
  const context = vm.createContext({ URLSearchParams, location: { search: query }, setTimeout() {},
    document: { addEventListener() {}, querySelectorAll() { return []; }, getElementById() { return area; }, createElement() { return { set textContent(v) { this.innerHTML = v; } }; } } });
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

function flujo(response, options = {}) {
  const storage = options.storage || new Map();
  const state = { requests: 0, prints: 0, shown: 0, hidden: 0, errors: [] };
  const buttons = [{ disabled: false }, { disabled: false }];
  const elements = {
    confirmarPlanificacionForm: { action: '/Pedido/ConfirmarPlanificacion' },
    planificador: { dataset: { usuario: 'test', soloReimpresion: String(!!options.recovery) } },
    estadoGuardado: {}, restaurarPlanBtn: {}, avisoReimpresion: {}, 'action-bar': { style: {} }, 'selected-count': {},
    confirmarImpresionModal: { classList: { contains() { return state.shown > state.hidden; } }, addEventListener(name, callback) { state.afterHidden = callback; } }, produccionConfirmada: { style: { display: 'none' } }, 'print-area': {}
  };
  const context = vm.createContext({ URLSearchParams, location: { search: options.query || '' }, structuredClone,
    localStorage: { getItem(key) { return storage.get(key); }, setItem(key, value) { storage.set(key, value); } },
    setTimeout(fn) { fn(); }, window: { print() { state.prints++; } },
    alert(message) { state.errors.push(message); }, FormData: class { constructor(form) { this.form = form; } },
    async fetch(url, options) {
      state.requests++;
      assert.equal(url, '/Pedido/ConfirmarPlanificacion');
      assert.equal(options.method, 'POST');
      assert.equal(options.body.form, elements.confirmarPlanificacionForm);
      return response();
    },
    bootstrap: { Modal: { getOrCreateInstance() { return { show() { state.shown++; }, hide() { state.hidden++; if (!options.deferHide) state.afterHidden?.(); } }; } } },
    document: {
      addEventListener() {}, querySelectorAll(selector) { return selector.includes('.Revision') ? [{ value: options.revision || 'r1' }] : selector === '.batch-check:checked' ? (state.selected || []).map(id => ({ dataset: { id } })) : buttons; }, getElementById(id) { return elements[id]; },
      createElement() { return { set textContent(v) { this.innerHTML = v; } }; }
    }
  });
  vm.runInContext(source.replace('let productosOriginales = [];', 'let productosOriginales = ' + JSON.stringify(options.products ?? products) + ';'), context);
  return { state, buttons, elements, storage, run(code) { return vm.runInContext(code, context); } };
}
const products = [1, 2].map(id => ({ idProducto: id, nombreProducto: 'Pan ' + id, masaKey: 'integral', nombreMasa: 'Integral', pesoMasaTotal: id * 1000, cantidadUnidades: id * 2, ingredientes: [{ idInsumo: 1, nombre: 'Harina', gramos: id * 500, unidad: 'g' }] }));
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


test('conserva combinaciones, divisiones y preparación anterior después de recargar', () => {
  const f = flujo(ok, { query: '?anteriores=1:150' });
  f.run('init()');
  f.elements['div-1'] = { value: '2' };
  f.run('dividir(1)');
  f.state.selected = [3, 2];
  f.run('combinar()');
  const saved = f.run('JSON.stringify(amasadas)');
  const reload = flujo(ok, { storage: f.storage, query: '?anteriores=1:150' });
  reload.run('init()');
  assert.equal(reload.run('JSON.stringify(amasadas)'), saved);
  assert.equal(reload.run('anteriores.get(1)'), 150);
  assert.equal(reload.run('soloReimpresion'), false);
});

test('después de confirmar recupera el plan aun sin pendientes y no vuelve a confirmar', async () => {
  const f = flujo(ok);
  f.run('init()');
  f.state.selected = [1, 2];
  f.run('combinar()');
  await f.run('confirmarEImprimir()');
  const reload = flujo(ok, { storage: f.storage, products: [] });
  reload.run('init(); solicitarImpresion()');
  await reload.run('confirmarEImprimir()');
  assert.equal(reload.run('amasadas.length'), 1);
  assert.equal(reload.run('amasadas[0].productos.length'), 2);
  assert.equal(reload.state.requests, 0);
  assert.equal(reload.state.prints, 2);
});

test('un plan antiguo no reemplaza nuevos pendientes; recuperación manual solo reimprime', async () => {
  const f = flujo(ok);
  f.run('init()');
  f.state.selected = [1, 2];
  f.run('combinar()');
  const reload = flujo(ok, { storage: f.storage, revision: 'nueva' });
  reload.run('init()');
  assert.equal(reload.run('amasadas.length'), 2);
  reload.run('restaurarPlan(false)');
  assert.equal(reload.run('amasadas.length'), 1);
  await reload.run('confirmarEImprimir()');
  assert.equal(reload.state.requests, 0);
});

test('recuperación de cantidades producidas y respuestas perdidas nunca duplican confirmación al reabrir', async () => {
  const recovered = flujo(ok, { recovery: true });
  recovered.run('init()');
  await recovered.run('confirmarEImprimir()');
  assert.equal(recovered.state.requests, 0);
  const f = flujo(() => { throw new Error('Respuesta perdida'); });
  f.run('init()');
  await f.run('confirmarEImprimir()');
  const reload = flujo(ok, { storage: f.storage });
  reload.run('init()');
  await reload.run('confirmarEImprimir()');
  assert.equal(reload.state.requests, 0);
});

test('espera a que desaparezca el fondo del modal antes de imprimir', () => {
  const f = flujo(ok, { deferHide: true });
  f.run('init(); solicitarImpresion(); soloImprimir()');
  assert.equal(f.state.prints, 0);
  f.state.afterHidden();
  assert.equal(f.state.prints, 1);
  assert.match(view, /\.modal-backdrop[^}]+display: none !important/);
  assert.match(view, /-webkit-text-fill-color: #000 !important/);
});

test('una copia dañada no impide abrir un nuevo plan', () => {
  const storage = new Map([['masa-viva:plan-amasadas:v1:test', '{invalido']]);
  const f = flujo(ok, { storage });
  f.run('init()');
  assert.equal(f.run('amasadas.length'), 2);
});test('recargar la reconstrucción conserva las agrupaciones ya corregidas', () => {  const f = flujo(ok, { recovery: true });  f.run('init()');  f.state.selected = [1, 2];  f.run('combinar()');  const reload = flujo(ok, { recovery: true, storage: f.storage });  reload.run('init()');  assert.equal(reload.run('amasadas.length'), 1);  assert.equal(reload.run('soloReimpresion'), true);});
