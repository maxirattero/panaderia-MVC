(function () {
    const costoEl = document.querySelector('[data-costo]');
    if (!costoEl) return;

    let costo = parseFloat(costoEl.dataset.costo) || 0;

    const precioFinal   = document.getElementById('PrecioFinal');
    const precioReventa = document.getElementById('PrecioReventa');
    const cmvFinal      = document.getElementById('cmvFinal');
    const markupFinal   = document.getElementById('markupFinal');
    const cmvReventa    = document.getElementById('cmvReventa');
    const markupReventa = document.getElementById('markupReventa');

    function calcularDesde(fuente, precioEl, cmvEl, markupEl) {
        if (costo <= 0) return;
        const precio = parseFloat(precioEl.value) || 0;
        const cmv    = parseFloat(cmvEl.value)    || 0;
        const markup = parseFloat(markupEl.value) || 0;

        if (fuente === 'precio' && precio > 0) {
            cmvEl.value    = (costo / precio * 100).toFixed(2);
            markupEl.value = ((precio - costo) / costo * 100).toFixed(2);
        } else if (fuente === 'cmv' && cmv > 0) {
            const p        = costo / (cmv / 100);
            precioEl.value = Math.round(p);
            markupEl.value = ((p - costo) / costo * 100).toFixed(2);
        } else if (fuente === 'markup') {
            const p        = costo * (1 + markup / 100);
            precioEl.value = Math.round(p);
            cmvEl.value    = (costo / p * 100).toFixed(2);
        }
    }

    precioFinal.addEventListener('input',
        () => calcularDesde('precio', precioFinal, cmvFinal, markupFinal));
    cmvFinal.addEventListener('input',
        () => calcularDesde('cmv', precioFinal, cmvFinal, markupFinal));
    markupFinal.addEventListener('input',
        () => calcularDesde('markup', precioFinal, cmvFinal, markupFinal));

    precioReventa.addEventListener('input',
        () => calcularDesde('precio', precioReventa, cmvReventa, markupReventa));
    cmvReventa.addEventListener('input',
        () => calcularDesde('cmv', precioReventa, cmvReventa, markupReventa));
    markupReventa.addEventListener('input',
        () => calcularDesde('markup', precioReventa, cmvReventa, markupReventa));

    document.addEventListener('mv:presentacion', e => {
        costo = e.detail;
        if (costo <= 0) { cmvFinal.value = markupFinal.value = cmvReventa.value = markupReventa.value = ''; return; }
        calcularDesde('precio', precioFinal, cmvFinal, markupFinal);
        calcularDesde('precio', precioReventa, cmvReventa, markupReventa);
    });
    if (parseFloat(precioFinal.value) > 0)
        calcularDesde('precio', precioFinal, cmvFinal, markupFinal);
    if (parseFloat(precioReventa.value) > 0)
        calcularDesde('precio', precioReventa, cmvReventa, markupReventa);
})();
