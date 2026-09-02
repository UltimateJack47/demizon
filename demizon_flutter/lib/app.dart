import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'core/router.dart';
import 'core/theme.dart';

class DemizonApp extends ConsumerWidget {
  const DemizonApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final router = ref.watch(routerProvider);

    return MaterialApp.router(
      title: 'Demizon',
      debugShowCheckedModeBanner: false,
      routerConfig: router,
      theme: DemizonTheme.light,
      darkTheme: DemizonTheme.dark,
      // MAUI verze měla v App.xaml.cs:25-26 natvrdo Light, protože styly
      // pro dark mode nebyly dodělané. Tady je téma kompletní pro obě varianty,
      // takže se respektuje systémové nastavení.
      themeMode: ThemeMode.system,
      locale: const Locale('cs', 'CZ'),
      supportedLocales: const [Locale('cs', 'CZ')],
      localizationsDelegates: const [
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
    );
  }
}
