import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/auth/auth_controller.dart';
import '../../core/notifications/device_registration.dart';
import '../../core/notifications/notification_navigation.dart';
import '../../core/providers.dart';
import '../../core/routes.dart';
import '../costumes/costume_logic.dart';

/// Spodní navigace se čtyřmi taby — protějšek `Demizon.Maui/AppShell.xaml`. Správce
/// krojárky má navíc pátý tab „Krojárka“; je to poslední větev, takže ostatním stačí
/// ho nevykreslit a indexy prvních čtyř se nemění.
///
/// V MAUI se kolem Shellu muselo bojovat s Android toolbarem: `AppShell.xaml.cs:34-78`
/// prolézal nativní view tree a skrýval toolbar pětkrát po sobě s časovači, protože
/// ho MAUI během layoutu pokaždé znovu vytvořil. Tady je to prostě Scaffold.
class MainShell extends ConsumerStatefulWidget {
  const MainShell({required this.navigationShell, super.key});

  final StatefulNavigationShell navigationShell;

  @override
  ConsumerState<MainShell> createState() => _MainShellState();
}

class _MainShellState extends ConsumerState<MainShell> {
  @override
  void initState() {
    super.initState();
    // Protějšek `MarkReadyAndFlush` + `NotificationSyncService.SyncAsync`
    // po auto-loginu (`App.xaml.cs`) a po ručním přihlášení (`LoginViewModel`).
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      ref.read(notificationNavigationProvider).markReadyAndFlush();
      unawaited(syncDeviceRegistration(ref.read(apiClientProvider)));
    });
  }

  StatefulNavigationShell get navigationShell => widget.navigationShell;

  static const _destinations = <NavigationDestination>[
    NavigationDestination(
      icon: Icon(Icons.event_available_outlined),
      selectedIcon: Icon(Icons.event_available),
      label: 'Docházka',
    ),
    NavigationDestination(
      icon: Icon(Icons.calendar_month_outlined),
      selectedIcon: Icon(Icons.calendar_month),
      label: 'Akce',
    ),
    NavigationDestination(
      icon: Icon(Icons.music_note_outlined),
      selectedIcon: Icon(Icons.music_note),
      label: 'Tance',
    ),
    NavigationDestination(
      icon: Icon(Icons.person_outline),
      selectedIcon: Icon(Icons.person),
      label: 'Profil',
    ),
  ];

  static const _keeperDestination = NavigationDestination(
    icon: Icon(Icons.checkroom_outlined),
    selectedIcon: Icon(Icons.checkroom),
    label: 'Krojárka',
  );

  @override
  Widget build(BuildContext context) {
    final isKeeper = ref.watch(authControllerProvider).session?.roles.contains(kCostumeKeeperRole) ?? false;
    return Scaffold(
      body: navigationShell,
      bottomNavigationBar: NavigationBar(
        // Kdo o roli správce přišel, smí na tabu zůstat jen do dalšího přechodu.
        selectedIndex: navigationShell.currentIndex.clamp(0, isKeeper ? 4 : 3),
        destinations: [..._destinations, if (isKeeper) _keeperDestination],
        onDestinationSelected: (index) => navigationShell.goBranch(
          index,
          // Druhý tap na už aktivní tab vrátí větev na její kořen.
          initialLocation: index == navigationShell.currentIndex,
        ),
      ),
    );
  }
}

/// Cesty kořenů jednotlivých tabů, v pořadí odpovídajícím [MainShell._destinations].
const shellBranchRoots = <String>[
  AppRoutes.attendance,
  AppRoutes.events,
  AppRoutes.dances,
  AppRoutes.profile,
  AppRoutes.costumeStore,
];
