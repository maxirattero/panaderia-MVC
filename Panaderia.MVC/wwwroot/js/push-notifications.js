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

    const actualizarEstado = (activa = false) => {
        button.title = Notification.permission === "denied"
            ? "Notificaciones bloqueadas: revisá los permisos del sitio"
            : activa ? "Notificaciones activadas. Hacé clic para probarlas" : "Activar o reparar notificaciones";
        button.setAttribute("aria-label", button.title);
        button.classList.toggle("notificaciones-activas", Boolean(activa));
    };

    const esperarRegistro = async () => {
        let timer;
        try {
            return await Promise.race([
                navigator.serviceWorker.ready,
                new Promise((_, reject) => {
                    timer = setTimeout(() => reject(new Error("El servicio de notificaciones no inició. Recargá la página e intentá nuevamente.")), 10000);
                })
            ]);
        } finally {
            clearTimeout(timer);
        }
    };

    const guardarSuscripcion = async subscription => {
        const response = await fetch("/NotificacionPush/Suscribir", {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "X-CSRF-TOKEN": antiforgeryToken
            },
            signal: AbortSignal.timeout(15000),
            body: JSON.stringify(subscription.toJSON())
        });
        if (response.redirected || response.status === 401 || response.status === 403) {
            throw new Error("Volvé a iniciar sesión como administrador para activar las notificaciones.");
        }
        if (!response.ok) throw new Error("No se pudo guardar la suscripción. Recargá la página e intentá nuevamente.");
    };

    const base64UrlToUint8Array = value => {
        const padding = "=".repeat((4 - value.length % 4) % 4);
        const base64 = (value + padding).replace(/-/g, "+").replace(/_/g, "/");
        const raw = atob(base64);
        return Uint8Array.from(raw, character => character.charCodeAt(0));
    };

    const suscripcionVigente = subscription => {
        if (!subscription || (subscription.expirationTime && subscription.expirationTime <= Date.now())) return false;
        const key = base64UrlToUint8Array(publicKey);
        const previousKey = subscription.options?.applicationServerKey;
        return previousKey && previousKey.byteLength === key.length
            && new Uint8Array(previousKey).every((value, index) => value === key[index]);
    };

    button.addEventListener("click", async () => {
        if (Notification.permission === "denied") {
            actualizarEstado();
            alert("Chrome bloqueó las notificaciones de este sitio. Abrí el icono junto a la dirección → Configuración del sitio → Notificaciones → Permitir. Después recargá y tocá la campanita.");
            return;
        }

        button.disabled = true;
        try {
            const permission = Notification.permission === "granted"
                ? "granted" : await Notification.requestPermission();
            if (permission !== "granted") {
                actualizarEstado();
                alert("No se activaron las notificaciones. Permitilas en la configuración de este sitio y volvé a tocar la campanita.");
                return;
            }

            const registration = await esperarRegistro();
            let subscription = await registration.pushManager.getSubscription();
            const key = base64UrlToUint8Array(publicKey);
            if (subscription && !suscripcionVigente(subscription)) {
                await subscription.unsubscribe();
                subscription = null;
            }
            if (!subscription) {
                subscription = await registration.pushManager.subscribe({
                    userVisibleOnly: true,
                    applicationServerKey: key
                });
            }

            await guardarSuscripcion(subscription);
            actualizarEstado(true);
            await registration.showNotification("Masa Viva · Notificación de prueba", {
                body: "Este dispositivo tiene una suscripción guardada para recibir avisos de pedidos.",
                icon: "/img/pwa-icon-192.png",
                data: { url: "/Pedido" }
            });
            alert("Suscripción guardada. Enviamos una notificación de prueba en este dispositivo. Si no la ves, revisá las notificaciones de Chrome en Windows y el modo No molestar.");
        } catch (error) {
            actualizarEstado();
            console.error("No se pudieron activar las notificaciones", error);
            alert(error.message || "No se pudieron activar las notificaciones. Revisá la conexión e intentá nuevamente.");
        } finally {
            button.disabled = false;
        }
    });

    actualizarEstado();
    // Restaurar el registro en el servidor sin volver a pedir permiso ni crear
    // suscripciones nuevas hasta que el usuario toque la campanita.
    if (Notification.permission === "granted") {
        button.disabled = true;
        (async () => {
            try {
                const registration = await esperarRegistro();
                const subscription = await registration.pushManager.getSubscription();
                if (suscripcionVigente(subscription)) {
                    await guardarSuscripcion(subscription);
                    actualizarEstado(true);
                }
            } catch {
                actualizarEstado();
            } finally {
                button.disabled = false;
            }
        })();
    }
})();
