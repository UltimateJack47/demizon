import 'package:flutter/material.dart';
import 'package:mobile_scanner/mobile_scanner.dart';

/// Pole na kód kusu nebo QR člena: kamera, nebo ručně. Ruční cesta je rovnocenná —
/// ošoupaný štítek i bluetooth čtečka, která kód „napíše“ i s Enterem.
class CodeScanField extends StatefulWidget {
  const CodeScanField({required this.onCode, this.label = 'Kód kusu nebo QR člena', super.key});

  final ValueChanged<String> onCode;
  final String label;

  @override
  State<CodeScanField> createState() => _CodeScanFieldState();
}

class _CodeScanFieldState extends State<CodeScanField> {
  final _controller = TextEditingController();

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  void _submit() {
    final text = _controller.text.trim();
    _controller.clear();
    if (text.isNotEmpty) widget.onCode(text);
  }

  Future<void> _scan() async {
    final text = await Navigator.of(context).push<String>(
      MaterialPageRoute(builder: (_) => const _ScannerPage(), fullscreenDialog: true),
    );
    if (text != null && text.isNotEmpty) widget.onCode(text);
  }

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Expanded(
          child: TextField(
            controller: _controller,
            decoration: InputDecoration(
              labelText: widget.label,
              border: const OutlineInputBorder(),
              isDense: true,
              suffixIcon: IconButton(icon: const Icon(Icons.keyboard_return), onPressed: _submit),
            ),
            textInputAction: TextInputAction.done,
            onSubmitted: (_) => _submit(),
          ),
        ),
        const SizedBox(width: 8),
        FilledButton.icon(
          // The theme's full-width minimum size would ask a Row for infinite width.
          style: FilledButton.styleFrom(minimumSize: const Size(0, 48)),
          onPressed: _scan,
          icon: const Icon(Icons.qr_code_scanner),
          label: const Text('Skenovat'),
        ),
      ],
    );
  }
}

/// Kamera přes celou obrazovku; vrátí první načtený QR a zavře se.
class _ScannerPage extends StatefulWidget {
  const _ScannerPage();

  @override
  State<_ScannerPage> createState() => _ScannerPageState();
}

class _ScannerPageState extends State<_ScannerPage> {
  final _controller = MobileScannerController(formats: const [BarcodeFormat.qrCode]);
  bool _done = false;

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  void _onDetect(BarcodeCapture capture) {
    if (_done) return;
    final value = capture.barcodes.map((b) => b.rawValue).whereType<String>().firstOrNull;
    if (value == null) return;
    // Kamera vidí štítek desítkykrát za sekundu; vrátit se má jen jednou.
    _done = true;
    Navigator.of(context).pop(value);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Naskenuj QR')),
      body: MobileScanner(
        controller: _controller,
        onDetect: _onDetect,
        errorBuilder: (context, error) => Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Text(
              'Kameru se nepodařilo spustit (${error.errorCode.name}). Povol přístup ke kameře, nebo napiš kód ručně.',
              textAlign: TextAlign.center,
            ),
          ),
        ),
      ),
    );
  }
}
