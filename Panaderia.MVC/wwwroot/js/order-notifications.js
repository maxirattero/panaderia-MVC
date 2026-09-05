(() => {
    const root = document.getElementById("notificacionesPedidos");
    if (!root) return;
    const bell = document.getElementById("btnPedidosNuevos");
    const badge = document.getElementById("contadorPedidosNuevos");
    const list = document.getElementById("listaPedidosNuevos");
    const status = document.getElementById("estadoPedidosNuevos");
    const mark = document.getElementById("marcarPedidosVistos");
    const storageKey = "mv_pedidos_vistos_" + root.dataset.userId;
    const parseId = value => Number.isSafeInteger(Number(value)) && Number(value) >= 0 ? Number(value) : 0;
    let lastSeen = 0;
    try { lastSeen = parseId(localStorage.getItem(storageKey)); } catch { /* Disponible durante esta página. */ }
    let snapshot = null;
    let busy = false;
    let stopped = false;
    const money = new Intl.NumberFormat("es-AR", { style: "currency", currency: "ARS", maximumFractionDigits: 0 });

    function render() {
        const count = snapshot.nuevos;
        badge.hidden = count === 0;
        badge.textContent = count > 99 ? "99+" : String(count);
        const label = count ? `${count} pedido${count === 1 ? " nuevo" : "s nuevos"}` : "Pedidos nuevos";
        bell.setAttribute("aria-label", label);
        bell.title = label;
        mark.disabled = count === 0;
        status.textContent = count ? `${count} pedido${count === 1 ? " nuevo" : "s nuevos"} sin ver`
            : snapshot.pedidos.length ? "Estás al día." : "No hay pedidos en los últimos 7 días.";
        list.replaceChildren();
        for (const pedido of snapshot.pedidos) {
            const link = document.createElement("a");
            link.className = "pedido-notificacion" + (pedido.id > lastSeen ? " nuevo" : "");
            link.href = root.dataset.detailUrl + "?id=" + encodeURIComponent(pedido.id);
            const title = document.createElement("strong");
            title.textContent = `Pedido #${pedido.id} · ${pedido.cliente.trim()}`;
            const detail = document.createElement("small");
            detail.textContent = `${money.format(pedido.montoTotal)} · ${new Date(pedido.fechaCreacion).toLocaleString("es-AR")}`;
            link.append(title, detail);
            list.append(link);
        }
    }

    async function refresh() {
        if (busy || stopped) return;
        busy = true;
        const requestedSeen = lastSeen;
        try {
            const response = await fetch(root.dataset.feedUrl + "?ultimoVisto=" + lastSeen, {
                cache: "no-store", signal: AbortSignal.timeout(15000)
            });
            if (response.redirected || response.status === 401 || response.status === 403) {
                stopped = true;
                throw new Error("Iniciá sesión como administrador y recargá para ver los pedidos.");
            }
            if (!response.ok) throw new Error("No se pudieron actualizar los pedidos. Reintentamos en unos segundos.");
            const data = await response.json();
            if (requestedSeen === lastSeen) {
                snapshot = data;
                render();
            }
        } catch (error) {
            status.textContent = error.name === "TimeoutError" || error.name === "TypeError"
                ? "Sin conexión. Reintentamos en unos segundos." : error.message;
        } finally {
            busy = false;
            if (requestedSeen !== lastSeen) refresh();
        }
    }

    mark.addEventListener("click", () => {
        if (!snapshot || busy) return;
        lastSeen = Math.max(lastSeen, snapshot.ultimoId);
        try { localStorage.setItem(storageKey, String(lastSeen)); } catch { /* No impedir marcar en esta página. */ }
        snapshot.nuevos = 0;
        render();
    });
    root.addEventListener("show.bs.dropdown", refresh);
    window.addEventListener("focus", refresh);
    window.addEventListener("online", refresh);
    window.addEventListener("storage", event => {
        if (event.key !== storageKey) return;
        lastSeen = parseId(event.newValue);
        refresh();
    });
    document.addEventListener("visibilitychange", () => { if (!document.hidden) refresh(); });
    setInterval(() => { if (!document.hidden) refresh(); }, 15000);
    refresh();
})();
