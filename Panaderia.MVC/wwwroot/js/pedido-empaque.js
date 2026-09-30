function aplicarEmpaquePredeterminado(fila) {
    const producto = fila.querySelector('.prod-select').selectedOptions[0];
    const empaque = fila.querySelector('.empaque-select');
    const valor = producto?.dataset.empaque || '';
    if (empaque.tomselect) empaque.tomselect.setValue(valor);
    else empaque.value = valor;
    fila.querySelector('.pedido-etiqueta-check').checked = producto?.dataset.etiqueta === 'true';
}
