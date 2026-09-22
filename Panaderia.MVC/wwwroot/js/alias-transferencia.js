document.querySelectorAll('.alias-transferencia').forEach(contenedor => {
    const boton = contenedor.querySelector('.alias-copiar');
    const estado = contenedor.querySelector('.alias-estado');
    const alias = contenedor.querySelector('[data-alias]').textContent.trim();

    boton.addEventListener('click', async () => {
        estado.textContent = '';
        try {
            if (!navigator.clipboard?.writeText) throw new Error('Portapapeles no disponible');
            await navigator.clipboard.writeText(alias);
            estado.textContent = '¡Alias copiado!';
        } catch {
            // Compatibilidad con navegadores sin acceso a Clipboard API.
            const campo = document.createElement('textarea');
            campo.value = alias;
            campo.readOnly = true;
            campo.style.position = 'fixed';
            campo.style.opacity = '0';
            document.body.appendChild(campo);
            try {
                campo.select();
                campo.setSelectionRange(0, alias.length);
                if (!document.execCommand('copy')) throw new Error('No se pudo copiar');
                estado.textContent = '¡Alias copiado!';
            } catch {
                estado.textContent = 'No se pudo copiar. Mantené presionado o seleccioná el alias para copiarlo.';
            } finally {
                campo.remove();
                boton.focus({ preventScroll: true });
            }
        }
    });
});
