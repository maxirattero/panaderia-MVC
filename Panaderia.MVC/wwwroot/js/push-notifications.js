(() => {
    const button = document.getElementById("btnNotificacionesPush");
    if (!button || !("serviceWorker" in navigator) || !("PushManager" in window) || !("Notification" in window)) {
        button?.classList.add("d-none");
        return;
    }

    const publicKey = button.dataset.vapidPublicKey;
    const antiforgeryToken = button.dataset.antiforgeryToken;
    if (!publicKey || !antiforgeryToken) {
        button.classList.add("d-none");
        return;
    }

    const actualizarEstado = async () => {
        const registration = await navigator.serviceWorker.ready;
        const subscription = await registration.pushManager.getSubscription();
        const activa = Notification.permission === "granted" && subscription;
        button.title = activa ? "Notificaciones activadas" : "Activar notificaciones";
        button.setAttribute("aria-label", button.title);
        button.classList.toggle("notificaciones-activas", Boolean(activa));
    };

    const base64UrlToUint8Array = value => {
        const padding = "=".repeat((4 - value.length % 4) % 4);
        const base64 = (value + padding).replace(/-/g, "+").replace(/_/g, "/");
        const raw = atob(base64);
        return Uint8Array.from(raw, character => character.charCodeAt(0));
    };

    button.addEventListener("click", async () => {
        if (Notification.permission === "denied") {
            alert("Las notificaciones están bloqueadas para Masa Viva. Habilitalas desde los permisos del navegador.");
            return;
        }

        try {
            const permission = await Notification.requestPermission();
            if (permission !== "granted") return;

            const registration = await navigator.serviceWorker.ready;
            let subscription = await registration.pushManager.getSubscription();
            if (!subscription) {
                subscription = await registration.pushManager.subscribe({
                    userVisibleOnly: true,
                    applicationServerKey: base64UrlToUint8Array(publicKey)
                });
            }

            const response = await fetch("/NotificacionPush/Suscribir", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "X-CSRF-TOKEN": antiforgeryToken
                },
                body: JSON.stringify(subscription.toJSON())
            });
            if (!response.ok) throw new Error();

            await actualizarEstado();
        } catch {
            alert("No se pudieron activar las notificaciones. Revisá la conexión e intentá nuevamente.");
        }
    });

    actualizarEstado().catch(() => { });
})();
