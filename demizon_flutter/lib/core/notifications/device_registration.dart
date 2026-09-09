import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:permission_handler/permission_handler.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../../api/api_client.dart';
import '../../models/models.dart';

/// Klíč v `SharedPreferences` — stejný, jaký používalo MAUI
/// (`Preferences.Default.Get("notifications_enabled", false)`).
const kNotificationsPrefKey = 'notifications_enabled';

/// Platforma posílaná na server při registraci zařízení.
/// MAUI bylo jen pro Android a posílalo natvrdo `"android"`.
const kDevicePlatform = 'android';

/// Kanál, který musí sedět na `FcmService` (`ChannelId = "demizon_channel"`)
/// i na `MainActivity.EnsureNotificationChannel` v MAUI.
const kNotificationChannelId = 'demizon_channel';

/// Přepis `NotificationSyncService.SyncAsync`: srovná stav na serveru
/// s tím, co uživatel skutečně povolil v systému.
///
/// Best-effort — MAUI chybu jen logovalo. Bez Firebase (chybí
/// `google-services.json`) se tiše vrátí.
Future<void> syncDeviceRegistration(ApiClient api) async {
  try {
    if (Firebase.apps.isEmpty) return;

    final prefs = await SharedPreferences.getInstance();
    final savedPref = prefs.getBool(kNotificationsPrefKey) ?? false;
    final granted = await Permission.notification.isGranted;

    final token = await FirebaseMessaging.instance.getToken();
    if (token == null || token.isEmpty) return;

    final request = RegisterDeviceRequest(
      token: token,
      platform: kDevicePlatform,
    );

    if (savedPref && !granted) {
      await api.unregisterDevice(request);
      await prefs.setBool(kNotificationsPrefKey, false);
    } else if (savedPref && granted) {
      await api.registerDevice(request);
    }
  } catch (_) {
    // Synchronizace je best-effort.
  }
}
