import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../features/attendance/all_members_attendance_screen.dart';
import '../features/attendance/attendance_screen.dart';
import '../features/attendance/attendance_stats_screen.dart';
import '../features/attendance/member_attendance_detail_controller.dart';
import '../features/attendance/member_attendance_detail_screen.dart';
import '../features/auth/login_screen.dart';
import '../features/dances/dance_detail_screen.dart';
import '../features/dances/dances_screen.dart';
import '../features/events/create_event_screen.dart';
import '../features/events/edit_event_screen.dart';
import '../features/events/event_detail_screen.dart';
import '../features/events/events_screen.dart';
import '../features/gallery/gallery_screen.dart';
import '../features/gallery/photo_viewer_screen.dart';
import '../features/profile/change_password_screen.dart';
import '../features/profile/edit_profile_screen.dart';
import '../features/profile/profile_screen.dart';
import '../features/shell/main_shell.dart';
import '../features/shell/splash_screen.dart';
import 'auth/auth_controller.dart';
import 'routes.dart';

final _rootKey = GlobalKey<NavigatorState>();

/// Router aplikace — protějšek `Demizon.Maui/AppShell.xaml` + `AppRoutes.cs`.
///
/// Proti MAUI verzi odpadá trojí ruční synchronizace, kterou si tam vynutil Shell:
/// registrace v DI (`MauiProgram.cs:45-78`), konstanta v `AppRoutes.cs` a
/// `Routing.RegisterRoute` v `AppShell.xaml.cs:9-19`. Tady je vše na jednom místě.
final routerProvider = Provider<GoRouter>((ref) {
  return GoRouter(
    navigatorKey: _rootKey,
    initialLocation: AppRoutes.splash,
    redirect: (context, state) {
      final auth = ref.read(authControllerProvider);
      final location = state.matchedLocation;

      // Dokud se obnovuje uložená session, drž uživatele na splashi —
      // jinak by přihlášenému probleskla přihlašovací obrazovka.
      if (auth.isRestoring) {
        return location == AppRoutes.splash ? null : AppRoutes.splash;
      }

      if (!auth.isAuthenticated) {
        return location == AppRoutes.login ? null : AppRoutes.login;
      }

      // Přihlášen: ze splashe i z loginu pokračuj do aplikace.
      if (location == AppRoutes.login || location == AppRoutes.splash) {
        return AppRoutes.attendance;
      }
      return null;
    },
    refreshListenable: ref.watch(authRefreshListenableProvider),
    routes: [
      GoRoute(
        path: AppRoutes.splash,
        builder: (context, state) => const SplashScreen(),
      ),
      GoRoute(
        path: AppRoutes.login,
        builder: (context, state) => const LoginScreen(),
      ),

      // Galerie je push přes celou obrazovku, mimo taby — stejně jako v MAUI,
      // kde na ni vedla samostatná routa z detailu tance i z profilu.
      GoRoute(
        path: AppRoutes.gallery,
        parentNavigatorKey: _rootKey,
        builder: (context, state) => const GalleryScreen(),
        routes: [
          GoRoute(
            path: 'viewer',
            parentNavigatorKey: _rootKey,
            builder: (context, state) {
              final args = state.extra as PhotoViewerArgs?;
              return PhotoViewerScreen(args: args);
            },
          ),
        ],
      ),

      StatefulShellRoute.indexedStack(
        builder: (context, state, navigationShell) =>
            MainShell(navigationShell: navigationShell),
        branches: [
          // ── Docházka ──────────────────────────────────────────
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: AppRoutes.attendance,
                builder: (context, state) => const AttendanceScreen(),
                routes: [
                  GoRoute(
                    path: 'overview',
                    builder: (context, state) =>
                        const AllMembersAttendanceScreen(),
                  ),
                  GoRoute(
                    path: 'stats',
                    builder: (context, state) => const AttendanceStatsScreen(),
                  ),
                  GoRoute(
                    path: 'member',
                    // Cíl se předává přes `extra`, protože nese víc než id
                    // (člen + akce NEBO datum zkoušky). Když chybí — deep link,
                    // hot restart — vrať se na přehled místo pádu.
                    redirect: (context, state) =>
                        state.extra is MemberAttendanceTarget
                            ? null
                            : AppRoutes.attendanceOverview,
                    builder: (context, state) => MemberAttendanceDetailScreen(
                      target: state.extra! as MemberAttendanceTarget,
                    ),
                  ),
                ],
              ),
            ],
          ),

          // ── Akce ──────────────────────────────────────────────
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: AppRoutes.events,
                builder: (context, state) => const EventsScreen(),
                routes: [
                  // POŘADÍ JE ZÁVAZNÉ: 'create' musí předcházet ':id',
                  // jinak by se "create" napasovalo jako id.
                  GoRoute(
                    path: 'create',
                    builder: (context, state) => const CreateEventScreen(),
                  ),
                  GoRoute(
                    path: ':id',
                    builder: (context, state) {
                      // Zkouška přichází jako `/events/0?rehearsalDate=…`
                      // (viz rehearsalDetailPath v attendance_controller.dart).
                      final raw = state.uri.queryParameters['rehearsalDate'];
                      final rehearsalDate =
                          raw == null ? null : DateTime.tryParse(raw);
                      return EventDetailScreen(
                        eventId: int.parse(state.pathParameters['id']!),
                        rehearsalDate: rehearsalDate,
                      );
                    },
                    routes: [
                      GoRoute(
                        path: 'edit',
                        builder: (context, state) => EditEventScreen(
                          eventId: int.parse(state.pathParameters['id']!),
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ],
          ),

          // ── Tance ─────────────────────────────────────────────
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: AppRoutes.dances,
                builder: (context, state) => const DancesScreen(),
                routes: [
                  GoRoute(
                    path: ':id',
                    builder: (context, state) => DanceDetailScreen(
                      danceId: int.parse(state.pathParameters['id']!),
                    ),
                  ),
                ],
              ),
            ],
          ),

          // ── Profil ────────────────────────────────────────────
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: AppRoutes.profile,
                builder: (context, state) => const ProfileScreen(),
                routes: [
                  GoRoute(
                    path: 'edit',
                    builder: (context, state) => const EditProfileScreen(),
                  ),
                  GoRoute(
                    path: 'password',
                    builder: (context, state) => const ChangePasswordScreen(),
                  ),
                ],
              ),
            ],
          ),
        ],
      ),
    ],
  );
});
