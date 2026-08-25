// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// ============================================================
// Selects con búsqueda (Tom Select)
// ------------------------------------------------------------
// Convierte en "select con búsqueda" a todo <select> del admin con muchas
// opciones: se escribe dentro del mismo campo y el desplegable se va achicando.
//
// - No hay que tocar las vistas: se aplica solo al cargar la página, y también
//   a los selects que aparecen después (filas de pedidos, recetas, compras).
// - Los selects cortos (Masa, Bolsa, estados) quedan como están.
// - Para excluir uno puntual: <select data-sin-buscador="true">
// - Para cambiar el texto de ayuda: <select data-placeholder="Buscar cliente...">
//
// Librería en wwwroot/lib/tom-select (MIT). Se carga desde _Layout.
// ============================================================
(function () {
    // Con menos opciones que esto, el buscador estorba más de lo que ayuda
    const MINIMO_OPCIONES = 8;

    // ¿El select está adentro de algo que recorta (overflow distinto de visible)?
    // Pasa en las tablas de Receta / Sub-receta / Pedido: el <table> vive dentro de
    // un .table-responsive (overflow auto) y de una .card con overflow-hidden, así
    // que el desplegable queda cortado y hay que scrollear ADENTRO de la tabla para
    // ver las opciones. En esos casos el desplegable se cuelga del <body>.
    function tieneAncestroQueRecorta(select) {
        let nodo = select.parentElement;
        while (nodo && nodo !== document.body) {
            const cs = getComputedStyle(nodo);
            if (cs.overflowX !== 'visible' || cs.overflowY !== 'visible') return true;
            nodo = nodo.parentElement;
        }
        return false;
    }

    // Ubica el desplegable pegado al campo; si no entra abajo, lo abre hacia arriba.
    function ubicarDesplegable(ts) {
        ts.positionDropdown();
        const dd = ts.dropdown;
        const rc = ts.control.getBoundingClientRect();
        const alto = dd.offsetHeight;
        const espacioAbajo = window.innerHeight - rc.bottom;
        if (alto > espacioAbajo && rc.top > espacioAbajo) {
            dd.style.top = (rc.top + window.scrollY - alto) + 'px';
        }
    }

    function esBuscable(select) {
        if (!(select instanceof HTMLSelectElement)) return false;
        if (select.tomselect) return false;                    // ya inicializado
        if (select.multiple) return false;
        if (select.dataset.sinBuscador === 'true') return false;
        return select.options.length >= MINIMO_OPCIONES;
    }

    function inicializar(select) {
        if (typeof TomSelect === 'undefined' || !esBuscable(select)) return;

        // Si el select tiene una opción vacía ("-- Seleccionar cliente --"),
        // la usamos de texto guía y la dejamos elegible para poder volver a vacío.
        const opcionVacia = select.querySelector('option[value=""]');
        const textoGuia = select.dataset.placeholder
            || (opcionVacia ? opcionVacia.textContent.trim() : 'Buscar o elegir...');

        const sinElegir = select.value === '';

        const opciones = {
            create: false,
            allowEmptyOption: !!opcionVacia,                   // deja volver a vacío en campos opcionales
            maxOptions: null,                                  // sin tope: listamos todo el catálogo
            placeholder: textoGuia,
            sortField: [{ field: '$order' }],                  // respeta el orden que manda el servidor
            render: {
                no_results: function (data, escape) {
                    return '<div class="no-results">Sin resultados para "' + escape(data.input) + '"</div>';
                }
            }
        };

        // Adentro de tablas/cards que recortan, el desplegable se cuelga del <body>
        // para que se vea entero sin tener que scrollear la sección.
        if (tieneAncestroQueRecorta(select)) {
            let reubicar = null;

            opciones.dropdownParent = 'body';

            opciones.onDropdownOpen = function () {
                const ts = this;
                ubicarDesplegable(ts);
                reubicar = function () { ubicarDesplegable(ts); };
                window.addEventListener('scroll', reubicar, true);
                window.addEventListener('resize', reubicar);
            };

            opciones.onDropdownClose = function () {
                if (!reubicar) return;
                window.removeEventListener('scroll', reubicar, true);
                window.removeEventListener('resize', reubicar);
                reubicar = null;
            };
        }

        const ts = new TomSelect(select, opciones);

        // Sin elegir todavía: mostramos solo el texto guía, no la opción vacía como si
        // fuera una selección (evita que el campo quede de dos renglones).
        if (sinElegir) ts.clear(true);
    }

    function inicializarTodos(raiz) {
        (raiz || document).querySelectorAll('select').forEach(inicializar);
    }

    document.addEventListener('DOMContentLoaded', function () {
        inicializarTodos(document);

        // Filas que se agregan después de cargar la página
        new MutationObserver(function (mutaciones) {
            mutaciones.forEach(function (m) {
                m.addedNodes.forEach(function (nodo) {
                    if (nodo.nodeType !== 1) return;
                    if (nodo instanceof HTMLSelectElement) inicializar(nodo);
                    else inicializarTodos(nodo);
                });
            });
        }).observe(document.body, { childList: true, subtree: true });
    });

    // Por si alguna vista necesita inicializar a mano un bloque nuevo
    window.inicializarSelectsBuscables = inicializarTodos;
})();
