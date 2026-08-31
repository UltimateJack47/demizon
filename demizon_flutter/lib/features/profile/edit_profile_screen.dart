import 'package:demizon/core/providers.dart';
import 'package:demizon/core/theme.dart';
import 'package:demizon/models/models.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

/// Přepis `Pages/EditProfilePage.xaml` + `ViewModels/EditProfileViewModel.cs`.
///
/// Formulář má krátký, čistě lokální život (načti → uprav → ulož → zpět),
/// takže si stav drží sám a nemá vlastní Riverpod controller.
class EditProfileScreen extends ConsumerStatefulWidget {
  const EditProfileScreen({super.key});

  @override
  ConsumerState<EditProfileScreen> createState() => _EditProfileScreenState();
}

class _EditProfileScreenState extends ConsumerState<EditProfileScreen> {
  final _name = TextEditingController();
  final _surname = TextEditingController();
  final _email = TextEditingController();

  bool _isBusy = true;
  String? _errorMessage;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _name.dispose();
    _surname.dispose();
    _email.dispose();
    super.dispose();
  }

  /// Protějšek `EditProfileViewModel.LoadAsync`.
  Future<void> _load() async {
    setState(() => _isBusy = true);
    try {
      final profile = await ref.read(apiClientProvider).getMyProfile();
      if (!mounted) return;
      _name.text = profile.name;
      _surname.text = profile.surname;
      _email.text = profile.email ?? '';
    } catch (_) {
      if (mounted) {
        setState(() => _errorMessage = 'Nepodařilo se načíst profil.');
      }
    } finally {
      if (mounted) setState(() => _isBusy = false);
    }
  }

  /// Protějšek `EditProfileViewModel.SaveAsync`.
  Future<void> _save() async {
    setState(() => _errorMessage = null);

    if (_name.text.trim().isEmpty) {
      setState(() => _errorMessage = 'Jméno je povinné.');
      return;
    }

    if (_surname.text.trim().isEmpty) {
      setState(() => _errorMessage = 'Příjmení je povinné.');
      return;
    }

    setState(() => _isBusy = true);
    try {
      final email = _email.text.trim();
      await ref.read(apiClientProvider).updateMyProfile(
            UpdateProfileRequest(
              name: _name.text.trim(),
              surname: _surname.text.trim(),
              email: email.isEmpty ? null : email,
            ),
          );
      if (mounted) context.pop();
    } catch (_) {
      if (mounted) {
        setState(() => _errorMessage = 'Nepodařilo se uložit profil.');
      }
    } finally {
      if (mounted) setState(() => _isBusy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Upravit profil')),
      body: ListView(
        padding: const EdgeInsets.all(20),
        children: [
          _LabeledField(
            label: 'Jméno',
            controller: _name,
            hintText: 'Jméno',
            textCapitalization: TextCapitalization.words,
          ),
          const SizedBox(height: 16),
          _LabeledField(
            label: 'Příjmení',
            controller: _surname,
            hintText: 'Příjmení',
            textCapitalization: TextCapitalization.words,
          ),
          const SizedBox(height: 16),
          _LabeledField(
            label: 'E-mail (volitelné)',
            controller: _email,
            hintText: 'email@example.com',
            keyboardType: TextInputType.emailAddress,
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
            onPressed: _isBusy ? null : _save,
            child: const Text('Uložit'),
          ),
          const SizedBox(height: 20),
          if (_isBusy) const Center(child: CircularProgressIndicator()),
        ],
      ),
    );
  }
}

class _LabeledField extends StatelessWidget {
  const _LabeledField({
    required this.label,
    required this.controller,
    required this.hintText,
    this.keyboardType,
    this.textCapitalization = TextCapitalization.none,
  });

  final String label;
  final TextEditingController controller;
  final String hintText;
  final TextInputType? keyboardType;
  final TextCapitalization textCapitalization;

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
          keyboardType: keyboardType,
          textCapitalization: textCapitalization,
          decoration: InputDecoration(hintText: hintText),
        ),
      ],
    );
  }
}
