import 'package:firebase_core/firebase_core.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/date_symbol_data_local.dart';

import 'app.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  // Protějšek inicializace v Platforms/Android/MainApplication.cs.
  // TODO(verify): vyžaduje firebase_options.dart z `flutterfire configure`.
  try {
    await Firebase.initializeApp();
  } catch (e) {
    debugPrint('Firebase se nepodařilo inicializovat: $e');
  }

  await initializeDateFormatting('cs_CZ');

  runApp(const ProviderScope(child: DemizonApp()));
}
