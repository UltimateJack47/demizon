import 'package:demizon/core/theme.dart';
import 'package:demizon/features/costumes/widgets/code_scan_field.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('pole skenu se v tématu appky vykreslí', (tester) async {
    // Regrese: téma dává FilledButton minimální šířku přes celý řádek a tlačítko
    // „Skenovat“ v Row pak chtělo nekonečnou šířku — výdej i příjem byly prázdné.
    await tester.pumpWidget(MaterialApp(
      theme: DemizonTheme.light,
      home: Scaffold(body: CodeScanField(onCode: (_) {})),
    ));

    expect(tester.takeException(), isNull);
    expect(find.text('Skenovat'), findsOneWidget);
  });

  test('tmavý režim má světlý text, světlý tmavý', () {
    final dark = DemizonTheme.dark.extension<DemizonPalette>()!;
    final light = DemizonTheme.light.extension<DemizonPalette>()!;

    expect(dark.textPrimary.computeLuminance(), greaterThan(0.8));
    expect(light.textPrimary.computeLuminance(), lessThan(0.1));
    expect(DemizonTheme.dark.appBarTheme.foregroundColor, dark.textPrimary);
  });
}
