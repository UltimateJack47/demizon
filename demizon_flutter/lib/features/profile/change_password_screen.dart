import 'package:demizon/core/providers.dart';
import 'package:demizon/core/theme.dart';
import 'package:demizon/models/models.dart';
import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

/// Přepis `Pages/ChangePasswordPage.xaml` + `ChangePasswordViewModel.cs`.
class ChangePasswordScreen extends ConsumerStatefulWidget {
  const ChangePasswordScreen({super.key});

  @override
  ConsumerState<ChangePasswordScreen> createState() =>
      _ChangePasswordScreenState();
}

class _ChangePasswordScreenState extends ConsumerState<ChangePasswordScreen> {
  final _current = TextEditingController();
  final _newPassword = TextEditingController();
  final _confirm = TextEditingController();

  bool _isBusy = false;
  String? _errorMessage;

  @override
  void dispose() {
    _current.dispose();
    _newPassword.dispose();
    _confirm.dispose();
    super.dispose();
  }

  /// Protějšek `ChangePasswordViewModel.ChangeAsync`.
  ///
  /// Validace jsou převzaté 1:1. MAUI po úspěchu čekalo `Task.Delay(1500)`,
  /// aby uživatel stihl přečíst hlášku — tady ji nese SnackBar, takže se
  /// naviguje hned.
  Future<void> _change() async {
    setState(() => _errorMessage = null);

    if (_current.text.trim().isEmpty) {
      setState(() => _errorMessage = 'Zadejte aktuální heslo.');
      return;
    }

    if (_newPassword.text.trim().isEmpty) {
      setState(() => _errorMessage = 'Zadejte nové heslo.');
      return;
    }

    if (_newPassword.text.length < 4) {
      setState(() => _errorMessage = 'Nové heslo musí mít alespoň 4 znaky.');
      return;
    }

    if (_newPassword.text != _confirm.text) {
      setState(() => _errorMessage = 'Nová hesla se neshodují.');
      return;
    }

    setState(() => _isBusy = true);
    try {
      await ref.read(apiClientProvider).changePassword(
            ChangePasswordRequest(
              currentPassword: _current.text,
              newPassword: _newPassword.text,
            ),
          );

      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Heslo bylo úspěšně změněno.')),
      );
      context.pop();
    } on DioException catch (e) {
      // Server vrací 400, když aktuální heslo nesedí.
      final message = e.response?.statusCode == 400
          ? 'Aktuální heslo je nesprávné.'
          : 'Nepodařilo se změnit heslo.';
      if (mounted) setState(() => _errorMessage = message);
    } catch (_) {
      if (mounted) {
        setState(() => _errorMessage = 'Nepodařilo se změnit heslo.');
      }
    } finally {
      if (mounted) setState(() => _isBusy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Změna hesla')),
      body: ListView(
        padding: const EdgeInsets.all(20),
        children: [
          _PasswordField(
            label: 'Aktuální heslo',
            hintText: 'Zadejte aktuální heslo',
            controller: _current,
          ),
          const SizedBox(height: 16),
          _PasswordField(
            label: 'Nové heslo',
            hintText: 'Zadejte nové heslo',
            controller: _newPassword,
          ),
          const SizedBox(height: 16),
          _PasswordField(
            label: 'Potvrzení nového hesla',
            hintText: 'Zadejte nové heslo znovu',
            controller: _confirm,
          ),
          if (_errorMessage != null) ...[
            const SizedBox(height: 16),
            Text(
              _errorMessage!,
              style: const TextStyle(fontSize: 14, color: DemizonColors.error),
            ),
          ],
          const SizedBox(height: 24),
          FilledButton(
            onPressed: _isBusy ? null : _change,
            child: const Text('Změnit heslo'),
          ),
          const SizedBox(height: 20),
          if (_isBusy) const Center(child: CircularProgressIndicator()),
        ],
      ),
    );
  }
}

class _PasswordField extends StatelessWidget {
  const _PasswordField({
    required this.label,
    required this.hintText,
    required this.controller,
  });

  final String label;
  final String hintText;
  final TextEditingController controller;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          label,
          style: const TextStyle(
            fontSize: 13,
            color: DemizonColors.textSecondary,
          ),
        ),
        const SizedBox(height: 4),
        TextField(
          controller: controller,
          obscureText: true,
          decoration: InputDecoration(hintText: hintText),
        ),
      ],
    );
  }
}
