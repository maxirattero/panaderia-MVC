(() => {
    const form = document.getElementById('seleccionPedidos');
    if (!form) return;
    const search = document.getElementById('filtroTexto');
    const rows = [...document.querySelectorAll('.table-custom > tbody > tr[data-pedido-id]')];
    const cards = [...document.querySelectorAll('.pedido-card')];
    const checkboxes = [...document.querySelectorAll('.seleccionar-pedido')];
    const selectAll = document.getElementById('seleccionarVisibles');
    const clear = document.getElementById('limpiarSeleccion');
    const buttons = [...form.querySelectorAll('button[type="submit"]')];
    const selected = new Set();
    let submitting = false;
    const visibleIds = () => rows.filter(row => row.style.display !== 'none').map(row => row.dataset.pedidoId);
    const normalize = text => (text || '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase();

    function sync() {
        checkboxes.forEach(box => { box.checked = selected.has(box.dataset.id); });
        const visible = visibleIds();
        const count = visible.filter(id => selected.has(id)).length;
        selectAll.checked = visible.length > 0 && count === visible.length;
        selectAll.indeterminate = count > 0 && count < visible.length;
        selectAll.disabled = visible.length === 0 || submitting;
        clear.disabled = selected.size === 0 || submitting;
        buttons.forEach(button => { button.disabled = selected.size === 0 || submitting; });
        document.getElementById('contadorSeleccion').textContent = selected.size === 1 ? '1 seleccionado' : `${selected.size} seleccionados`;
        const inputs = [...selected].map(id => {
            const input = document.createElement('input');
            input.type = 'hidden';
            input.name = 'Ids';
            input.value = id;
            return input;
        });
        document.getElementById('idsSeleccionados').replaceChildren(...inputs);
    }

    function filter() {
        const text = normalize(search.value);
        [...rows, ...cards].forEach(element => {
            const visible = normalize(element.dataset.busqueda).includes(text);
            element.style.display = visible ? '' : 'none';
            if (!visible) selected.delete(element.dataset.pedidoId);
            if (element.tagName === 'TR' && element.nextElementSibling?.classList.contains('collapse'))
                element.nextElementSibling.style.display = visible ? '' : 'none';
        });
        const count = visibleIds().length;
        document.getElementById('contadorVisible').textContent = count === 1 ? '1 pedido' : `${count} pedidos`;
        sync();
    }

    checkboxes.forEach(box => box.addEventListener('change', () => {
        if (box.checked) selected.add(box.dataset.id);
        else selected.delete(box.dataset.id);
        sync();
    }));
    selectAll.addEventListener('change', () => {
        visibleIds().forEach(id => { if (selectAll.checked) selected.add(id); else selected.delete(id); });
        sync();
    });
    clear.addEventListener('click', () => { selected.clear(); sync(); });
    search.addEventListener('input', filter);
    form.addEventListener('submit', event => {
        if (submitting || selected.size === 0 || !event.submitter) {
            event.preventDefault();
            return;
        }
        // Conservar la acción al deshabilitar los botones durante el envío.
        const action = document.createElement('input');
        action.type = 'hidden';
        action.name = 'Accion';
        action.value = event.submitter.value;
        form.append(action);
        submitting = true;
        sync();
    });
    window.addEventListener('pageshow', () => {
        submitting = false;
        form.querySelectorAll('input[name="Accion"]').forEach(input => input.remove());
        filter();
    });
    filter();
})();
