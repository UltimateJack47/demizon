import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import 'package:demizon/core/auth/auth_controller.dart';
import 'package:demizon/core/routes.dart';
import 'package:demizon/core/theme.dart';

/// Přepis `Demizon.Maui/Pages/LoginPage.xaml` + `ViewModels/LoginViewModel.cs`.
///
/// Rozložení: hero pruh s přechodem (320 dp) s logem a názvem souboru,
/// přes něj přesahující karta s formulářem.
///
/// V MAUI se logo načítalo přes `FileSystem.OpenAppPackageFileAsync` kvůli
/// chybě v načítání obrázků — ten workaround se neportuje, `Image.asset`
/// funguje přímo.
class LoginScreen extends ConsumerStatefulWidget {
  const LoginScreen({super.key});

  @override
  ConsumerState<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends ConsumerState<LoginScreen> {
  final _loginController = TextEditingController();
  final _passwordController = TextEditingController();

  bool _isBusy = false;
  String? _errorMessage;

  @override
  void dispose() {
    _loginController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  Future<void> _login() async {
    if (_isBusy) return;
    setState(() {
      _isBusy = true;
      _errorMessage = null;
    });

    try {
      // TODO(verify): očekávaný kontrakt `AuthController.login(login, password)`
      // — uloží tokeny přes TokenStorage a nastaví stav session.
      await ref
          .read(authControllerProvider.notifier)
          .login(_loginController.text, _passwordController.text);

      if (!mounted) return;
      // MAUI: navigation.GoToAsync(AppRoutes.MainTabs) == "//main/attendance".
      context.go(AppRoutes.attendance);
    } on DioException catch (e) {
      setState(() => _errorMessage = _messageFor(e));
    } catch (_) {
      setState(
        () => _errorMessage = 'Přihlášení selhalo. Zkuste to prosím znovu.',
      );
    } finally {
      if (mounted) setState(() => _isBusy = false);
    }
  }

  /// Protějšek tří `catch` větví v `LoginViewModel.LoginAsync`.
  String _messageFor(DioException e) {
    if (e.response?.statusCode == 401) {
      return 'Nesprávné přihlašovací jméno nebo heslo.';
    }
    return switch (e.type) {
      DioExceptionType.connectionError ||
      DioExceptionType.connectionTimeout ||
      DioExceptionType.sendTimeout ||
      DioExceptionType.receiveTimeout =>
        'Nelze se připojit k serveru. Zkontrolujte síťové připojení.',
      _ => 'Přihlášení selhalo. Zkuste to prosím znovu.',
    };
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      // MAUI má Title="Přihlášení", ale Shell na této stránce toolbar skrýval.
      // Hero pruh je zároveň hlavička, AppBar proto není.
      body: SingleChildScrollView(
        child: Stack(
          children: [
            const _Hero(),
            Padding(
              padding: const EdgeInsets.only(
                top: 280,
                left: 24,
                right: 24,
                bottom: 24,
              ),
              child: _buildCard(context),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildCard(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 28),
      decoration: BoxDecoration(
        color: isDark
            ? DemizonColors.cardBackgroundDark
            : DemizonColors.cardBackground,
        borderRadius: BorderRadius.circular(12),
        boxShadow: const [
          BoxShadow(
            color: Color(0x1F000000),
            offset: Offset(0, 4),
            blurRadius: 16,
          ),
        ],
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          TextField(
            controller: _loginController,
            enabled: !_isBusy,
            autocorrect: false,
            textInputAction: TextInputAction.next,
            decoration: const InputDecoration(
              hintText: '👤  Přihlašovací jméno',
            ),
          ),
          const SizedBox(height: 12),
          TextField(
            controller: _passwordController,
            enabled: !_isBusy,
            obscureText: true,
            textInputAction: TextInputAction.done,
            onSubmitted: (_) => _login(),
            decoration: const InputDecoration(hintText: 'Heslo'),
          ),
          const SizedBox(height: 12),
          if (_errorMessage != null) ...[
            Text(
              _errorMessage!,
              style: const TextStyle(color: Colors.red, fontSize: 13),
            ),
            const SizedBox(height: 8),
          ],
          const SizedBox(height: 4),
          SizedBox(
            height: 48,
            child: FilledButton(
              onPressed: _isBusy ? null : _login,
              child: _isBusy
                  ? const SizedBox(
                      width: 22,
                      height: 22,
                      child: CircularProgressIndicator(
                        strokeWidth: 2,
                        color: DemizonColors.primary,
                      ),
                    )
                  : const Text('Přihlásit se'),
            ),
          ),
        ],
      ),
    );
  }
}

class _Hero extends StatelessWidget {
  const _Hero();

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return Container(
      height: 320,
      padding: const EdgeInsets.only(top: 56, bottom: 72),
      decoration: const BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          colors: [DemizonColors.primary, DemizonColors.primaryDark],
        ),
      ),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Container(
            width: 100,
            height: 100,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: isDark
                  ? DemizonColors.cardBackgroundDark
                  : DemizonColors.cardBackground,
              border: Border.all(
                color: DemizonColors.textOnPrimary,
                width: 3,
              ),
            ),
            alignment: Alignment.center,
            child: ClipOval(
              child: Image.asset(
                // TODO(verify): soubor `assets/images/demizon_logo.jpg` je
                // potřeba do projektu doplnit (v MAUI byl `demizon_logo.png`
                // v Resources/Images).
                'assets/images/demizon_logo.jpg',
                width: 80,
                height: 80,
                fit: BoxFit.contain,
              ),
            ),
          ),
          const SizedBox(height: 8),
          const Text(
            'FS Demižón',
            style: TextStyle(
              fontSize: 28,
              fontWeight: FontWeight.bold,
              color: DemizonColors.textOnPrimary,
            ),
          ),
          const SizedBox(height: 8),
          const Opacity(
            opacity: 0.75,
            child: Text(
              'Strážnice',
              style: TextStyle(
                fontSize: 16,
                color: DemizonColors.textOnPrimary,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
