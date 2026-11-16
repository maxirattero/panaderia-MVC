(() => {
    const tabla = document.getElementById('materialesProducto');
    if (!tabla) return;
    const fuentes = JSON.parse(document.getElementById('insumosPresentacion').textContent);
    const costoEl = document.getElementById('costoTotalPresentacion');
    const receta = costoEl.dataset.tieneReceta === 'true';
    const costoBase = Number(costoEl.dataset.costoBase) || 0;
    const dinero = new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', maximumFractionDigits: 2 });
    const redondear = n => Math.round((n + Number.EPSILON) * 10000) / 10000;
    let siguienteId = tabla.querySelectorAll('tr').length;
    function recalcular() {
        const bolsa = Number(document.getElementById('IdEmpaquePredeterminado').value);
        const etiqueta = document.getElementById('EtiquetaPredeterminada').checked;
        let adicionales = 0, elegida = null, cantidadEtiqueta = 1;
        tabla.querySelectorAll('tr').forEach((fila, i) => {
            // Solo cambia el índice del formulario. Los IDs del buscador deben permanecer estables.
            fila.querySelectorAll('[name]').forEach(el => el.name = el.name.replace(/Materiales\[\d+\]/g, `Materiales[${i}]`));
            const fuente = fuentes.find(x => x.id === Number(fila.querySelector('.material-insumo').value));
            const cantidad = Number(fila.querySelector('.material-cantidad').value) || 0;
            let costo = 0, ayuda = '';
            if (fuente?.tipo === 2) {
                elegida = fuente; cantidadEtiqueta = cantidad;
                costo = etiqueta ? redondear(cantidad * redondear(fuente.costo)) : 0;
                ayuda = 'Se controla con «Incluir etiqueta por defecto».';
            } else if (fuente) {
                const aplicada = Math.max(0, cantidad - (bolsa === fuente.id ? 1 : 0));
                costo = redondear(aplicada * redondear(fuente.costo)); adicionales += costo;
                if (bolsa === fuente.id) ayuda = 'Una unidad ya está incluida en la bolsa principal.';
            }
            fila.querySelector('.material-subtotal').textContent = dinero.format(costo);
            fila.querySelector('.material-ayuda').textContent = ayuda;
        });
        const costoBolsa = redondear(fuentes.find(x => x.id === bolsa)?.costo || 0);
        const etiquetaHabitual = fuentes.filter(x => x.tipo === 2 && x.activo).sort((a,b) => a.id-b.id)[0];
        const costoEtiqueta = etiqueta ? redondear(redondear((elegida || etiquetaHabitual)?.costo || 0) * (elegida ? cantidadEtiqueta : 1)) : 0;
        document.getElementById('costoAdicionales').textContent = dinero.format(adicionales);
        document.getElementById('costoBolsaPrincipal').textContent = dinero.format(costoBolsa);
        document.getElementById('costoEtiquetaPresentacion').textContent = dinero.format(costoEtiqueta);
        const base = receta ? costoBase : Number(document.getElementById('CostoManual').value) || 0;
        const total = redondear(base + adicionales + costoBolsa + costoEtiqueta);
        costoEl.dataset.costo = String(total);
        costoEl.textContent = dinero.format(total);
        document.getElementById('calculadoraCosto').hidden = total <= 0;
        document.dispatchEvent(new CustomEvent('mv:presentacion', {detail:total}));
    }
    document.getElementById('agregarMaterial').addEventListener('click', () => {
        const fila = document.getElementById('materialTemplate').content.firstElementChild.cloneNode(true);
        const indice = tabla.querySelectorAll('tr').length;
        const id = siguienteId++;
        fila.querySelectorAll('[name], [id], [for]').forEach(el => {
            for (const atributo of ['name', 'id', 'for']) if (el.hasAttribute(atributo))
                el.setAttribute(atributo, el.getAttribute(atributo).replaceAll('__INDEX__', String(atributo === 'name' ? indice : id)));
        });
        tabla.appendChild(fila);
        window.inicializarSelectsBuscables?.(fila);
        recalcular();
        (fila.querySelector('.ts-control input') || fila.querySelector('select')).focus();
    });
    tabla.addEventListener('click', e => {
        const quitar = e.target.closest('.quitar-material');
        if (quitar) {
            const fila = quitar.closest('tr');
            fila.querySelector('.material-insumo').tomselect?.destroy();
            fila.remove(); recalcular();
        }
    });
    tabla.addEventListener('input', recalcular);
    tabla.addEventListener('change', recalcular);
    for (const id of ['IdEmpaquePredeterminado','EtiquetaPredeterminada','CostoManual']) document.getElementById(id).addEventListener('input', recalcular);
    recalcular();
})();
