const CACHE_NAME = "masaviva-static-v3";
const STATIC_ASSETS = [
    "/css/site.css",
    "/js/site.js",
    "/js/pwa.js",
    "/js/push-notifications.js",
    "/img/pwa-icon-192.png",
    "/img/logo-masaviva.png",
    "/img/logo-masaviva-blanco.png",
    "/favicon.ico",
    "/manifest.webmanifest",
    "/offline.html"
];

self.addEventListener("install", event => {
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then(cache => cache.addAll(STATIC_ASSETS))
            .then(() => self.skipWaiting())
    );
});

self.addEventListener("push", event => {
    const data = event.data ? event.data.json() : {};
    const title = data.title || "Masa Viva";
    const options = {
        body: data.body || "Tenés una novedad para revisar.",
        icon: "/img/logo-masaviva.png",
        badge: "/img/pwa-icon-192.png",
        data: { url: data.url || "/Pedido" }
    };

    event.waitUntil(self.registration.showNotification(title, options));
});

self.addEventListener("notificationclick", event => {
    event.notification.close();
    const destination = new URL(event.notification.data.url, self.location.origin).href;

    event.waitUntil(
        clients.matchAll({ type: "window", includeUncontrolled: true }).then(clientList => {
            const existing = clientList.find(client => client.url === destination);
            return existing ? existing.focus() : clients.openWindow(destination);
        })
    );
});

self.addEventListener("activate", event => {
    event.waitUntil(
        caches.keys()
            .then(keys => Promise.all(
                keys.filter(key => key.startsWith("masaviva-") && key !== CACHE_NAME)
                    .map(key => caches.delete(key))
            ))
            .then(() => self.clients.claim())
    );
});

self.addEventListener("fetch", event => {
    const { request } = event;

    // Los POST nunca se guardan: pedidos, cobros y producción necesitan conexión.
    if (request.method !== "GET") return;

    const url = new URL(request.url);
    if (url.origin !== self.location.origin) return;

    // Las páginas tienen datos dinámicos: se consultan siempre a la red.
    if (request.mode === "navigate") {
        event.respondWith(fetch(request).catch(() => caches.match("/offline.html")));
        return;
    }

    if (!STATIC_ASSETS.includes(url.pathname)) return;

    event.respondWith(
        caches.match(url.pathname).then(cached => {
            if (cached) return cached;

            return fetch(request).then(response => {
                if (response.ok) {
                    const copy = response.clone();
                    caches.open(CACHE_NAME).then(cache => cache.put(url.pathname, copy));
                }
                return response;
            });
        })
    );
});
