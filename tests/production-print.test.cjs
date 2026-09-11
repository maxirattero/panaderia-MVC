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
