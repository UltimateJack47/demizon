import 'dart:async';

import 'package:demizon/core/auth/auth_controller.dart';
import 'package:demizon/core/providers.dart';
import 'package:demizon/models/models.dart';
import 'package:dio/dio.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:permission_handler/permission_handler.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// Klíč v `SharedPreferences` — stejný, jaký používalo MAUI
/// (`Preferences.Default.Get("notifications_enabled", false)`).
const _notificationsPrefKey = 'notifications_enabled';

/// Platforma posílaná na server při registraci zařízení.
// TODO(verify): MAUI bylo jen pro Android a posílalo natvrdo "android".
// Až se přidá iOS build, ověř, co server očekává (`RegisterDeviceRequest`).
const _devicePlatform = 'android';

/// Verze aplikace. MAUI ji četlo z `AppInfo.Current.VersionString`.
// TODO(verify): ekvivalent je `package_info_plus`, který zatím není v
// pubspec.yaml. Do té doby drž hodnotu shodnou s `version:` v pubspec.yaml.
const kAppVersion = '1.0.0';

/// Stav obrazovky profilu — protějšek `ObservableProperty` polí
/// v `ProfileViewModel`.
class ProfileState {
  const ProfileState({
    required this.login,
    required this.role,
    required this.googleCalendarConnected,
    required this.notificationsEnabled,
    this.isTogglingNotifications = false,
    this.notificationError,
    this.testNotificationMessage,
  });

  final String login;
  final String role;
  final bool googleCalendarConnected;
  final bool notificationsEnabled;

  /// Nahrazuje `_handlingNotificationToggle` z MAUI (`ProfileViewModel.cs:37`).
  ///
  /// V MAUI šlo o obranu proti tomu, že zápis do `NotificationsEnabled` při
  /// rollbacku znovu vyvolal `OnNotificationsEnabledChanged`. Tady žádný
  /// takový zpětný signál není — `Switch.onChanged` je čistě uživatelský
  /// vstup. Příznak proto zůstává jen jako ochrana proti druhému ťuknutí
  /// během běžícího requestu a je součástí stavu, ne skryté proměnné.
  final bool isTogglingNotifications;

  final String? notificationError;
  final String? testNotificationMessage;

  ProfileState copyWith({
    String? login,
    String? role,
    bool? googleCalendarConnected,
    bool? notificationsEnabled,
    bool? isTogglingNotifications,
    String? notificationError,
    String? testNotificationMessage,
    bool clearNotificationError = false,
    bool clearTestNotificationMessage = false,
  }) {
    return ProfileState(
      login: login ?? this.login,
      role: role ?? this.role,
      googleCalendarConnected:
          googleCalendarConnected ?? this.googleCalendarConnected,
      notificationsEnabled: notificationsEnabled ?? this.notificationsEnabled,
      isTogglingNotifications:
          isTogglingNotifications ?? this.isTogglingNotifications,
      notificationError: clearNotificationError
          ? null
          : (notificationError ?? this.notificationError),
      testNotificationMessage: clearTestNotificationMessage
          ? null
          : (testNotificationMessage ?? this.testNotificationMessage),
    );
  }
}

final profileProvider = AsyncNotifierProvider<ProfileController, ProfileState>(
  ProfileController.new,
);

/// Přepis `ViewModels/ProfileViewModel.cs`.
class ProfileController extends AsyncNotifier<ProfileState> {
  @override
  Future<ProfileState> build() async {
    // Protějšek `TokenStorage.GetLoginAsync` / `GetRoleAsync` /
    // `GetIsGoogleCalendarConnectedAsync` (`core/auth/token_storage.dart`).
    final tokenStorage = ref.read(tokenStorageProvider);

    final login = await tokenStorage.login;
    final role = await tokenStorage.role;

    // Uložená preference musí souhlasit se skutečným systémovým oprávněním —
    // jinak by přepínač tvrdil "zapnuto" u aplikace bez povolení
    // (`ProfileViewModel.cs:50-57`).
    final prefs = await SharedPreferences.getInstance();
    final savedPref = prefs.getBool(_notificationsPrefKey) ?? false;
    final granted = await Permission.notification.isGranted;

    // Protějšek `NotificationSyncService.SyncAsync` — v MAUI se pouštěl
    // bez čekání (`_ = syncService.SyncAsync()`).
    unawaited(_syncDeviceRegistration(savedPref: savedPref, granted: granted));

    return ProfileState(
      login: login ?? '—',
      role: role ?? '—',
      googleCalendarConnected: await tokenStorage.gcalConnected,
      notificationsEnabled: savedPref && granted,
    );
  }

  /// Přepis `NotificationSyncService.SyncAsync`: srovná stav na serveru
  /// s tím, co uživatel skutečně povolil v systému.
  Future<void> _syncDeviceRegistration({
    required bool savedPref,
    required bool granted,
  }) async {
    try {
      final api = ref.read(apiClientProvider);
      final token = await FirebaseMessaging.instance.getToken();
      if (token == null || token.isEmpty) return;

      final request = RegisterDeviceRequest(
        token: token,
        platform: _devicePlatform,
      );

      if (savedPref && !granted) {
        // Oprávnění bylo mezitím odebráno — zruš registraci na serveru.
        await api.unregisterDevice(request);
        final prefs = await SharedPreferences.getInstance();
        await prefs.setBool(_notificationsPrefKey, false);
      } else if (savedPref && granted) {
        // Obnov registraci (FCM token se může měnit).
        await api.registerDevice(request);
      }
    } catch (_) {
      // Synchronizace je best-effort; MAUI chybu jen logovalo.
    }
  }

  /// Přepis `HandleNotificationToggleAsync` (`ProfileViewModel.cs:76-134`).
  ///
  /// Pořadí kroků je zachováno: oprávnění → FCM token → registrace na
  /// serveru → uložení preference. Při jakémkoli selhání se přepínač vrátí
  /// do původní polohy a zobrazí se hláška.
  Future<void> setNotificationsEnabled(bool enable) async {
    final current = state.valueOrNull;
    if (current == null) return;

    // Reentrancy guard: druhé ťuknutí během běžícího requestu se ignoruje.
    if (current.isTogglingNotifications) return;

    state = AsyncData(
      current.copyWith(
        notificationsEnabled: enable,
        isTogglingNotifications: true,
        clearNotificationError: true,
      ),
    );

    void fail(String message) {
      state = AsyncData(
        state.requireValue.copyWith(
          notificationsEnabled: !enable,
          isTogglingNotifications: false,
          notificationError: message,
        ),
      );
    }

    try {
      if (enable) {
        final status = await Permission.notification.request();
        if (!status.isGranted) {
          fail('Oprávnění k notifikacím zamítnuto.');
          return;
        }
      }

      String? fcmToken;
      try {
        fcmToken = await FirebaseMessaging.instance.getToken();
      } catch (e) {
        fail('FCM chyba: $e');
        return;
      }

      if (fcmToken == null || fcmToken.isEmpty) {
        fail(
          'FCM token nelze získat — zkontrolujte google-services.json a ApplicationId.',
        );
        return;
      }

      final api = ref.read(apiClientProvider);
      final request = RegisterDeviceRequest(
        token: fcmToken,
        platform: _devicePlatform,
      );

      if (enable) {
        await api.registerDevice(request);
      } else {
        await api.unregisterDevice(request);
      }

      final prefs = await SharedPreferences.getInstance();
      await prefs.setBool(_notificationsPrefKey, enable);

      state = AsyncData(
        state.requireValue.copyWith(isTogglingNotifications: false),
      );
    } catch (e) {
      fail('Chyba API: $e');
    }
  }

  /// Přepis `SendTestNotificationAsync` (`ProfileViewModel.cs:137`).
  Future<void> sendTestNotification() async {
    final current = state.valueOrNull;
    if (current == null) return;

    state = AsyncData(current.copyWith(clearTestNotificationMessage: true));

    try {
      await ref.read(apiClientProvider).sendTestNotification();
      state = AsyncData(
        state.requireValue.copyWith(
          testNotificationMessage: '✓ Notifikace odeslána',
        ),
      );
    } catch (e) {
      // MAUI rozlišovalo podle textu výjimky ("503" / "Firebase");
      // s Diem je stavový kód dostupný přímo.
      final isServerFirebaseProblem = e is DioException
          ? e.response?.statusCode == 503 ||
              (e.response?.data?.toString().contains('Firebase') ?? false)
          : e.toString().contains('503') || e.toString().contains('Firebase');

      state = AsyncData(
        state.requireValue.copyWith(
          testNotificationMessage: isServerFirebaseProblem
              ? '✗ Firebase není nakonfigurován na serveru'
              : '✗ Žádné registrované zařízení — nejdřív povol notifikace',
        ),
      );
    }
  }

  /// Přepis `LogoutAsync` (`ProfileViewModel.cs:166`).
  ///
  /// MAUI navíc volalo `NotificationNavigationService.Reset()` — obdoba
  /// (zahození čekajícího deep linku z notifikace) patří do notifikační
  /// vrstvy, ne sem.
  ///
  /// Po odhlášení přesměruje na přihlášení router podle stavu session
  /// (`core/router.dart` + `authControllerProvider`), ne tato metoda.
  Future<void> logout() => ref.read(authControllerProvider.notifier).logout();
}
