// Isolated UI preview using the real order template, scripts and styles.
// No database, authentication, migrations or order submissions.
// Run: node tests/order-editor-preview.cjs; open http://127.0.0.1:4178
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const root = path.join(__dirname, '../Panaderia.MVC');
const read = file => fs.readFileSync(path.join(root, file), 'utf8');

function preview(edit) {
    const source = read(`Views/Pedido/${edit ? 'Edit' : 'Create'}.cshtml`);
    let template = read('Views/Pedido/_ProductoPedidoTemplate.cshtml')
        .replace('@using Panaderia.Models.Entities', '')
        .replace(/@foreach \(var p[\s\S]*?\n\s*\}/, [
            '<option value="1" data-final="3500" data-reventa="2800" data-stock="5" data-por-encargo="false">Hummus MESTIZO Ajo Negro</option>',
            '<option value="2" data-final="4500" data-reventa="3800" data-stock="2" data-por-encargo="false">Prepizza de masa madre</option>',
            '<option value="3" data-final="6000" data-reventa="5000" data-stock="0" data-por-encargo="true">Pan de molde por encargo</option>',
            ...Array.from({ length: 5 }, (_, i) => `<option value="${i + 4}" data-final="1000" data-stock="0" data-por-encargo="true">Producto de prueba ${i + 4}</option>`)
        ].join(''))
        .replace(/@foreach \(var e[\s\S]*?\n\s*\}/, '<option value="1">Bolsa de papel</option>');
    const products = source.slice(source.indexOf('<h4'), source.indexOf('<div class="d-flex flex-column'));
    const script = source.slice(source.indexOf('<script>'), source.lastIndexOf('</script>') + 9)
        .replace(/@Html.Raw\([\s\S]*?CamelCase \}\)\)/, '[{"idProducto":1,"cantidad":2,"idEmpaque":1,"llevaEtiqueta":true}]');
    return `<!doctype html><html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
        <link rel="stylesheet" href="/lib/bootstrap/dist/css/bootstrap.min.css">
        <link rel="stylesheet" href="/lib/tom-select/css/tom-select.bootstrap5.min.css">
        <link rel="stylesheet" href="/css/site.css">
        <link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Literata:wght@400;700&amp;family=Plus+Jakarta+Sans:wght@400;600;700&amp;display=swap">
        <title>Prueba local de pedidos</title></head><body>
        <main class="container-fluid p-2 p-md-5" style="max-width:1200px">
        <h1 class="titulo-pagina">${edit ? 'Editar' : 'Nuevo'} Pedido</h1>
        <p>Vista de prueba · no guarda pedidos</p>
        <div class="card card-custom p-4"><form onsubmit="return false">
        <div class="row g-4"><label class="col-md-8">Cliente<select id="IdCliente" class="form-select"><option data-revendedor="false" data-descuento="0">Cliente de prueba</option><option data-revendedor="true" data-descuento="10">Revendedor de prueba</option></select></label>
        <label class="col-md-4">Descuento (%)<input id="DescuentoPorcentaje" class="form-control" type="number" value="0"></label></div>
        ${products}</form></div></main>${template}
        <script src="/lib/tom-select/js/tom-select.base.min.js"></script><script src="/js/site.js"></script>${script}</body></html>`;
}

const assets = new Set(['/lib/bootstrap/dist/css/bootstrap.min.css', '/lib/tom-select/css/tom-select.bootstrap5.min.css', '/css/site.css', '/lib/tom-select/js/tom-select.base.min.js', '/js/site.js']);
http.createServer((req, res) => {
    const url = new URL(req.url, 'http://127.0.0.1:4178');
    if (url.pathname === '/') {
        res.setHeader('Content-Type', 'text/html; charset=utf-8');
        res.end(preview(url.searchParams.has('edit')));
    } else if (assets.has(url.pathname)) {
        res.setHeader('Content-Type', url.pathname.endsWith('.css') ? 'text/css' : 'text/javascript');
        res.end(read('wwwroot' + url.pathname));
    } else { res.writeHead(404); res.end(); }
}).listen(4178, '127.0.0.1', () => console.log('Order UI preview: http://127.0.0.1:4178 (add ?edit for Edit)'));
