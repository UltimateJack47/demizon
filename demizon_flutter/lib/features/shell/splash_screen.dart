import 'package:flutter/material.dart';

/// Zobrazuje se, dokud `AuthController` obnovuje uloženou session.
///
/// Bez ní by router při startu vyhodnotil `isAuthenticated == false` (stav je
/// ještě `AsyncLoading`) a probleskla by přihlašovací obrazovka i přihlášenému
/// uživateli.
class SplashScreen extends StatelessWidget {
  const SplashScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return const Scaffold(
      body: Center(child: CircularProgressIndicator()),
    );
  }
}
