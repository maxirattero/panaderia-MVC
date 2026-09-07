(() => {
    const form = document.getElementById("loginForm");
    if (!form) return;
    let submitting = false;
    form.addEventListener("submit", event => {
        if (submitting) {
            event.preventDefault();
            return;
        }
        submitting = true;
        const button = form.querySelector('button[type="submit"]');
        button.disabled = true;
        button.textContent = "Ingresando…";
    });
    // El historial móvil puede restaurar el formulario con un token de la sesión anterior.
    window.addEventListener("pageshow", event => {
        if (event.persisted) window.location.reload();
    });
})();
