import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'core/notifications/notification_navigation.dart';
import 'core/notifications/push_notifications.dart';
import 'core/router.dart';
import 'core/theme.dart';

class DemizonApp extends ConsumerStatefulWidget {
  const DemizonApp({super.key});

  @override
  ConsumerState<DemizonApp> createState() => _DemizonAppState();
}

class _DemizonAppState extends ConsumerState<DemizonApp> {
  @override
  void initState() {
    super.initState();
    // FCM listeneri až po prvním snímku — ProviderScope už drží navigaci.
    WidgetsBinding.instance.addPostFrameCallback((_) {
      unawaited(ref.read(pushNotificationsProvider).start());
    });
  }

  @override
  Widget build(BuildContext context) {
    final router = ref.watch(routerProvider);
    ref.read(notificationNavigationProvider).bind(
          (location) => router.go(location),
        );

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
