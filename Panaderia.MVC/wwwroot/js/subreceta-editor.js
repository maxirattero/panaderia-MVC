(() => {
    const editor = document.getElementById('editorSubReceta');
    const tbody = document.getElementById('detallesBody');
    const template = document.getElementById('filaTemplate');
    const campos = { '.id-insumo': 'IdInsumo', '.id-subreceta': 'IdSubRecetaIngrediente', '.inp-pct': 'PorcentajePanadero', '.inp-fija': 'CantidadFija' };
    function reindexar() {
        tbody.querySelectorAll('tr').forEach((tr, i) => {
            for (const [selector, nombre] of Object.entries(campos))
                tr.querySelector(selector).name = `Detalles[${i}].${nombre}`;
        });
    }
    function adaptar(tr) {
        const select = tr.querySelector('.sel-ingrediente');
        const [tipo, id] = select.value.split('-');
        tr.querySelector('.id-insumo').value = tipo === 'i' ? id : '';
        tr.querySelector('.id-subreceta').value = tipo === 's' ? id : '';
        const fija = select.selectedOptions[0]?.dataset.unidad === '2';
        const pct = tr.querySelector('.inp-pct');
        const cantidad = tr.querySelector('.inp-fija');
        pct.hidden = pct.disabled = fija;
        cantidad.hidden = cantidad.disabled = !fija;
        tr.querySelector('.tipo-label').textContent = fija ? 'Cantidad fija' : '% Panadero';
    }
    function agregar(detalle = {}) {
        const tr = template.content.cloneNode(true).querySelector('tr');
        const select = tr.querySelector('.sel-ingrediente');
        if (detalle.IdSubRecetaIngrediente) select.value = `s-${detalle.IdSubRecetaIngrediente}`;
        else if (detalle.IdInsumo) select.value = `i-${detalle.IdInsumo}`;
        tr.querySelector('.inp-pct').value = detalle.PorcentajePanadero ?? '';
        tr.querySelector('.inp-fija').value = detalle.CantidadFija ?? '';
        adaptar(tr);
        select.addEventListener('change', () => {
            adaptar(tr);
            tr.querySelector('.inp-pct').value = '';
            tr.querySelector('.inp-fija').value = '';
        });
        tr.querySelector('.btn-quitar').addEventListener('click', () => { tr.remove(); reindexar(); });
        tbody.appendChild(tr);
        reindexar();
    }
    JSON.parse(editor.dataset.detalles).forEach(agregar);
    document.getElementById('btnAgregar').addEventListener('click', () => agregar());
})();
