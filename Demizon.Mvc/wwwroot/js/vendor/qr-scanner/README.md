qr-scanner 1.4.2 (Nimiq, MIT — viz LICENSE), z npm balíčku `qr-scanner`, beze změn.
Soubory: `qr-scanner.min.js` (ES modul) a `qr-scanner-worker.min.js` (dekodér; knihovna si ho
načítá relativně, proto musí ležet vedle). Kde prohlížeč umí `BarcodeDetector`, worker se nepoužije.
Používá ho `../../costume-scanner.js`. Schváleno pro krojárku (docs/features/krojarka, O5).
