import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../core/routes.dart';

/// Spodní navigace se čtyřmi taby — protějšek `Demizon.Maui/AppShell.xaml`.
///
/// V MAUI se kolem Shellu muselo bojovat s Android toolbarem: `AppShell.xaml.cs:34-78`
/// prolézal nativní view tree a skrýval toolbar pětkrát po sobě s časovači, protože
/// ho MAUI během layoutu pokaždé znovu vytvořil. Tady je to prostě Scaffold.
class MainShell extends StatelessWidget {
  const MainShell({required this.navigationShell, super.key});

  final StatefulNavigationShell navigationShell;

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

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: navigationShell,
      bottomNavigationBar: NavigationBar(
        selectedIndex: navigationShell.currentIndex,
        destinations: _destinations,
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
];
