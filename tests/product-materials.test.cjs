const {test} = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

// Ejecuta la calculadora real sobre campos ficticios; no accede a la aplicación ni a una DB.
function fixture(materiales, bolsa = 3, etiqueta = true) {
    const fuentes = [{id:1,tipo:1,costo:200},{id:2,tipo:1,costo:60},{id:3,tipo:1,costo:100},
        {id:4,tipo:1,costo:150},{id:5,tipo:2,costo:10},{id:6,tipo:2,costo:20},{id:7,tipo:3,costo:5}].map(x=>({...x,activo:true}));
    const campo = value => ({value:String(value), checked:false, dataset:{}, listeners:{},
        addEventListener(type, fn) { this.listeners[type]=fn; }});
    const rows = materiales.map(([id,cantidad],i) => {
        const select=campo(id), qty=campo(cantidad), subtotal={}, ayuda={};
        select.name=`Materiales[${i}].IdInsumo`; qty.name=`Materiales[${i}].Cantidad`;
        return {select,qty,subtotal, querySelectorAll:()=>[select,qty],
            querySelector:s=>({'.material-insumo':select,'.material-cantidad':qty,'.material-subtotal':subtotal,'.material-ayuda':ayuda}[s])};
    });
    const tabla=campo(''); tabla.querySelectorAll=()=>rows;
    const fields = {materialesProducto:tabla,insumosPresentacion:{textContent:JSON.stringify(fuentes)},
        costoTotalPresentacion:{dataset:{tieneReceta:'false',costoBase:'1000',costo:'0'}},
        IdEmpaquePredeterminado:campo(bolsa),EtiquetaPredeterminada:campo(''),CostoManual:campo(1000),
        costoAdicionales:{},costoBolsaPrincipal:{},costoEtiquetaPresentacion:{},calculadoraCosto:{},agregarMaterial:campo(''),
        PrecioFinal:campo(3000),PrecioReventa:campo(2500),cmvFinal:campo(''),markupFinal:campo(''),cmvReventa:campo(''),markupReventa:campo('')};
    fields.EtiquetaPredeterminada.checked=etiqueta;
    const events={};
    const document={getElementById:id=>fields[id],querySelector:()=>fields.costoTotalPresentacion,
        addEventListener:(type,fn)=>events[type]=fn,dispatchEvent:e=>events[e.type]?.(e)};
    const ctx=vm.createContext({document,window:{},Intl,CustomEvent:class {constructor(type,{detail}) {this.type=type; this.detail=detail;}}});
    for(const file of ['producto-costos.js','producto-materiales.js'])
        vm.runInContext(fs.readFileSync(`Panaderia.MVC/wwwroot/js/${file}`,'utf8'),ctx);
    return {fields,rows,total:()=>Number(fields.costoTotalPresentacion.dataset.costo),recalcular:()=>tabla.listeners.input()};
}
test('costo completo actualiza CMV y margen sin modificar precios de venta',()=>{
    const f=fixture([[1,1],[2,1],[7,.5],[6,1]]);
    assert.equal(f.total(),1382.5);
    assert.equal(f.fields.cmvFinal.value,'46.08');
    assert.equal(f.fields.PrecioFinal.value,'3000');
    f.rows[0].qty.value='2'; f.recalcular();
    assert.equal(f.total(),1582.5);
    assert.equal(f.fields.PrecioFinal.value,'3000');
});
test('cambiar bolsa reemplaza su costo y destildar etiqueta la excluye',()=>{
    const f=fixture([[1,1],[2,1],[7,.5],[6,1]]);
    f.fields.IdEmpaquePredeterminado.value='4'; f.fields.EtiquetaPredeterminada.checked=false; f.recalcular();
    assert.equal(f.total(),1412.5);
    assert.match(f.rows[3].subtotal.textContent,/0,00/);
});
test('bolsa adicional coincidente suma una sola vez; dos envoltorios siguen siendo dos',()=>{
    const f=fixture([[3,1]],3,false); assert.equal(f.total(),1100);
    f.rows[0].qty.value='2'; f.recalcular(); assert.equal(f.total(),1200);
});
test('sin adicionales conserva bolsa y etiqueta habitual y recalcula costo base manual',()=>{
    const f=fixture([]); assert.equal(f.total(),1110);
    f.fields.CostoManual.value='1250'; f.recalcular(); assert.equal(f.total(),1360);
});
