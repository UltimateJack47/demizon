// Skenování QR kamerou pro krojárku. Volá se z Blazoru jako JS modul (CodeScanner.razor).
// Kamera potřebuje zabezpečený kontext (HTTPS nebo localhost).
import QrScanner from './vendor/qr-scanner/qr-scanner.min.js';

let scanner = null;
let lastText = null;
let lastAt = 0;

export async function start(video, dotNetRef) {
    stop();
    if (!(await QrScanner.hasCamera())) {
        return 'Zařízení nemá kameru. Napiš kód ručně.';
    }

    scanner = new QrScanner(video, result => {
        // Kamera vidí tentýž štítek desítkykrát za sekundu; Blazoru stačí jednou.
        const now = Date.now();
        if (result.data === lastText && now - lastAt < 2500) return;
        lastText = result.data;
        lastAt = now;
        dotNetRef.invokeMethodAsync('OnScanned', result.data);
    }, { preferredCamera: 'environment', highlightScanRegion: true, returnDetailedScanResult: true });

    try {
        await scanner.start();
        return null;
    } catch (e) {
        stop();
        return 'Kameru se nepodařilo spustit (' + e + '). Povol přístup ke kameře, nebo napiš kód ručně.';
    }
}

export function stop() {
    if (scanner) {
        scanner.destroy();
        scanner = null;
    }
}
