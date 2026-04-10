// Demizon Service Worker – Web Push notifikace
// Registruje se automaticky přes push-notifications.js

self.addEventListener('push', event => {
    if (!event.data) return;

    let data;
    try {
        data = event.data.json();
    } catch {
        data = { title: 'Demizon', body: event.data.text(), url: '/' };
    }

    const options = {
        body: data.body || '',
        icon: '/favicon.png',
        badge: '/favicon.png',
        data: { url: data.url || '/' },
        requireInteraction: false,
    };

    event.waitUntil(self.registration.showNotification(data.title || 'Demizon', options));
});

self.addEventListener('notificationclick', event => {
    event.notification.close();
    const raw = event.notification.data?.url || '/';
    // Povolit jen relativní cesty – ochrana proti javascript: a data: URL
    const url = (typeof raw === 'string' && raw.startsWith('/')) ? raw : '/';
    event.waitUntil(clients.openWindow(url));
});
