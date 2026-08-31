import 'package:demizon/core/routes.dart';
import 'package:demizon/core/theme.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import 'profile_controller.dart';

/// Přepis `Pages/ProfilePage.xaml`.
///
/// Barvy v XAML byly natvrdo zapsané hexy (#A8845E, #3E2723, …) — tady se
/// berou ze `core/theme.dart`, kde je stejná paleta pojmenovaná.
class ProfileScreen extends ConsumerWidget {
  const ProfileScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final profile = ref.watch(profileProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Profil')),
      body: profile.when(
        data: (state) => _ProfileBody(state: state),
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (_, __) => const Center(
          child: Padding(
            padding: EdgeInsets.all(20),
            child: Text(
              'Nepodařilo se načíst profil.',
              style: TextStyle(color: DemizonColors.error),
            ),
          ),
        ),
      ),
    );
  }
}

class _ProfileBody extends ConsumerWidget {
  const _ProfileBody({required this.state});

  final ProfileState state;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final controller = ref.read(profileProvider.notifier);

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        // ------------------------------------------------------- Hlavička
        _ProfileCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                'Přihlášen jako ${state.login}',
                style: const TextStyle(
                  fontSize: 18,
                  fontWeight: FontWeight.bold,
                  color: DemizonColors.textPrimary,
                ),
              ),
              const SizedBox(height: 6),
              Align(
                alignment: Alignment.centerLeft,
                child: Container(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                  decoration: BoxDecoration(
                    color: DemizonColors.primary,
                    borderRadius: BorderRadius.circular(8),
                  ),
                  child: Text(
                    state.role,
                    style: const TextStyle(fontSize: 12, color: Colors.white),
                  ),
                ),
              ),
            ],
          ),
        ),
        const SizedBox(height: 16),

        // ------------------------------------------------- Google Calendar
        _ProfileCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text(
                'Google Calendar',
                style: TextStyle(
                  fontSize: 16,
                  fontWeight: FontWeight.bold,
                  color: DemizonColors.textPrimary,
                ),
              ),
              const SizedBox(height: 8),
              Row(
                children: [
                  const Text(
                    'Stav:',
                    style: TextStyle(color: DemizonColors.textPrimary),
                  ),
                  const SizedBox(width: 8),
                  Text(
                    state.googleCalendarConnected ? 'Propojeno' : 'Nepropojeno',
                    style: TextStyle(
                      fontSize: 14,
                      fontWeight: FontWeight.bold,
                      color: state.googleCalendarConnected
                          ? DemizonColors.success
                          : DemizonColors.error,
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 8),
              const Text(
                'Propojení s Google Calendar probíhá přes webovou administraci na demizon.cz',
                style: TextStyle(
                  fontSize: 12,
                  color: DemizonColors.textSecondary,
                ),
              ),
            ],
          ),
        ),
        const SizedBox(height: 16),

        // ------------------------------------------------------ Notifikace
        _ProfileCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text(
                'Notifikace',
                style: TextStyle(
                  fontSize: 16,
                  fontWeight: FontWeight.bold,
                  color: DemizonColors.textPrimary,
                ),
              ),
              const SizedBox(height: 8),
              Row(
                children: [
                  const Expanded(
                    child: Text(
                      'Push notifikace',
                      style: TextStyle(color: DemizonColors.textPrimary),
                    ),
                  ),
                  Switch(
                    value: state.notificationsEnabled,
                    // Během běžícího přepnutí je přepínač neaktivní — druhé
                    // ťuknutí by jinak mohlo přepsat probíhající rollback.
                    onChanged: state.isTogglingNotifications
                        ? null
                        : controller.setNotificationsEnabled,
                  ),
                ],
              ),
              const SizedBox(height: 8),
              Text(
                state.notificationsEnabled
                    ? 'Notifikace jsou zapnuté'
                    : 'Notifikace jsou vypnuté',
                style: const TextStyle(
                  fontSize: 12,
                  color: DemizonColors.textSecondary,
                ),
              ),
              if (state.notificationError != null) ...[
                const SizedBox(height: 8),
                Text(
                  state.notificationError!,
                  style: const TextStyle(
                    fontSize: 12,
                    color: DemizonColors.error,
                  ),
                ),
              ],
              const SizedBox(height: 8),
              Align(
                alignment: Alignment.centerLeft,
                child: FilledButton(
                  style: FilledButton.styleFrom(
                    minimumSize: const Size(0, 36),
                    padding: const EdgeInsets.symmetric(horizontal: 12),
                    backgroundColor: DemizonColors.primary,
                    foregroundColor: Colors.white,
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(8),
                    ),
                  ),
                  onPressed: state.notificationsEnabled
                      ? controller.sendTestNotification
                      : null,
                  child: const Text(
                    '🔔 Test notifikace',
                    style: TextStyle(fontSize: 12),
                  ),
                ),
              ),
              if (state.testNotificationMessage != null) ...[
                const SizedBox(height: 8),
                Text(
                  state.testNotificationMessage!,
                  style: const TextStyle(
                    fontSize: 12,
                    color: DemizonColors.textSecondary,
                  ),
                ),
              ],
            ],
          ),
        ),
        const SizedBox(height: 16),

        // --------------------------------------------------- O aplikaci
        Center(
          child: Text(
            'Verze aplikace: $kAppVersion',
            style: const TextStyle(
              fontSize: 12,
              color: DemizonColors.textSecondary,
            ),
          ),
        ),
        const SizedBox(height: 16),

        // ------------------------------------------------------- Tlačítka
        FilledButton(
          style: FilledButton.styleFrom(
            backgroundColor: DemizonColors.primary,
            foregroundColor: Colors.white,
            textStyle: const TextStyle(fontWeight: FontWeight.bold),
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(12),
            ),
          ),
          onPressed: () => context.push(AppRoutes.profileEdit),
          child: const Text('Upravit profil'),
        ),
        const SizedBox(height: 16),
        OutlinedButton(
          style: OutlinedButton.styleFrom(
            minimumSize: const Size.fromHeight(48),
            foregroundColor: DemizonColors.primary,
            side: const BorderSide(color: DemizonColors.primary),
            textStyle: const TextStyle(fontWeight: FontWeight.bold),
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(12),
            ),
          ),
          onPressed: () => context.push(AppRoutes.changePassword),
          child: const Text('🔒 Změnit heslo'),
        ),
        const SizedBox(height: 24),
        OutlinedButton(
          style: OutlinedButton.styleFrom(
            minimumSize: const Size.fromHeight(48),
            foregroundColor: DemizonColors.error,
            side: const BorderSide(color: DemizonColors.error),
            textStyle: const TextStyle(fontWeight: FontWeight.bold),
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(12),
            ),
          ),
          // Přesměrování na přihlášení obstará router podle stavu session
          // (`core/router.dart`), obrazovka naviguje sama.
          onPressed: controller.logout,
          child: const Text('Odhlásit se'),
        ),
      ],
    );
  }
}

class _ProfileCard extends StatelessWidget {
  const _ProfileCard({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: EdgeInsets.zero,
      child: Padding(padding: const EdgeInsets.all(20), child: child),
    );
  }
}
