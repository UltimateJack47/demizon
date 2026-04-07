// Demizon – Web Push helper
// Volán z Blazor komponent přes JS Interop

window.demizonPush = {
    vapidPublicKey: null,

    // Registruje service worker a vrátí subscription jako JSON string
    async subscribe(vapidPublicKey) {
        if (!('serviceWorker' in navigator) || !('PushManager' in window)) {
            return null;
        }

        this.vapidPublicKey = vapidPublicKey;

        const registration = await navigator.serviceWorker.register('/service-worker.js');
        await navigator.serviceWorker.ready;

        const permission = await Notification.requestPermission();
        if (permission !== 'granted') return null;

        const subscription = await registration.pushManager.subscribe({
            userVisibleOnly: true,
            applicationServerKey: this._urlBase64ToUint8Array(vapidPublicKey),
        });

        return JSON.stringify(subscription);
    },

    // Odregistruje aktuální subscription
    async unsubscribe() {
        if (!('serviceWorker' in navigator)) return false;
        const registration = await navigator.serviceWorker.ready;
        const subscription = await registration.pushManager.getSubscription();
        if (!subscription) return false;
        return subscription.unsubscribe();
    },

    // Zjistí, zda je aktuálně aktivní subscription
    async getSubscription() {
        if (!('serviceWorker' in navigator)) return null;
        const registration = await navigator.serviceWorker.ready;
        const subscription = await registration.pushManager.getSubscription();
        return subscription ? JSON.stringify(subscription) : null;
    },

    // Převod VAPID public key z base64url na Uint8Array
    _urlBase64ToUint8Array(base64String) {
        const padding = '='.repeat((4 - base64String.length % 4) % 4);
        const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
        const rawData = atob(base64);
        return Uint8Array.from([...rawData].map(c => c.charCodeAt(0)));
    },

    // Podpora pro Append.Blazor.Notifications – zjistí stav povolení
    async getNotificationPermission() {
        if (!('Notification' in window)) return 'unsupported';
        return Notification.permission;
    }
};
