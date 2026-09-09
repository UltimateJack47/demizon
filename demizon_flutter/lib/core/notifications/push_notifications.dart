import 'dart:async';

import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/material.dart';
import 'package:flutter_local_notifications/flutter_local_notifications.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:permission_handler/permission_handler.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../../api/api_client.dart';
import '../../models/models.dart';
import '../providers.dart';
import 'device_registration.dart';
import 'notification_navigation.dart';
import 'notification_target.dart';

/// Barva notifikace — `notification_color` / FCM `Color = "#8B1A1A"`.
const _notificationColor = Color(0xFF8B1A1A);

/// FCM + lokální notifikace — přepis `MainActivity` (kanál, foreground
/// zobrazení, tap) a `NotificationNavigationService.Initialize`.
///
/// Bez `google-services.json` Firebase neběží; `start()` to pozná a
/// nenaslouchá. Aplikace jinak funguje dál (stejně jako dnešní `main.dart`).
class PushNotifications {
  PushNotifications({
    required NotificationNavigation navigation,
    required ApiClient Function() apiClient,
  })  : _navigation = navigation,
        _apiClient = apiClient;

  final NotificationNavigation _navigation;
  final ApiClient Function() _apiClient;
  final FlutterLocalNotificationsPlugin _local =
      FlutterLocalNotificationsPlugin();

  bool _started = false;
  StreamSubscription<RemoteMessage>? _onMessage;
  StreamSubscription<RemoteMessage>? _onOpened;
  StreamSubscription<String>? _onTokenRefresh;

  Future<void> start() async {
    if (_started) return;
    _started = true;

    await _initLocalNotifications();

    if (Firebase.apps.isEmpty) return;

    _onMessage = FirebaseMessaging.onMessage.listen(_onForegroundMessage);
    _onOpened = FirebaseMessaging.onMessageOpenedApp.listen((message) {
      _navigation.handle(message.data);
    });
    _onTokenRefresh =
        FirebaseMessaging.instance.onTokenRefresh.listen(_reregisterToken);

    final initial = await FirebaseMessaging.instance.getInitialMessage();
    if (initial != null) {
      _navigation.handle(initial.data);
    }
  }

  void dispose() {
    _onMessage?.cancel();
    _onOpened?.cancel();
    _onTokenRefresh?.cancel();
  }

  Future<void> _initLocalNotifications() async {
    const android = AndroidInitializationSettings('@drawable/ic_notification');
    await _local.initialize(
      const InitializationSettings(android: android),
      onDidReceiveNotificationResponse: (response) {
        _navigation.handlePayload(response.payload);
      },
    );

    final androidPlugin = _local
        .resolvePlatformSpecificImplementation<
            AndroidFlutterLocalNotificationsPlugin>();
    await androidPlugin?.createNotificationChannel(
      const AndroidNotificationChannel(
        kNotificationChannelId,
        'Demižón',
        description: 'Připomínky a oznámení Demižón',
        importance: Importance.defaultImportance,
      ),
    );

    // Cold start z lokální notifikace (foreground cesta, aplikace byla zabitá).
    final launch = await _local.getNotificationAppLaunchDetails();
    if (launch?.didNotificationLaunchApp == true) {
      _navigation.handlePayload(launch!.notificationResponse?.payload);
    }
  }

  Future<void> _onForegroundMessage(RemoteMessage message) async {
    final notification = message.notification;
    final title = notification?.title ?? 'Demižón';
    final body = notification?.body ?? '';
    final target = NotificationTarget.parse(message.data);

    const details = NotificationDetails(
      android: AndroidNotificationDetails(
        kNotificationChannelId,
        'Demižón',
        channelDescription: 'Připomínky a oznámení Demižón',
        importance: Importance.defaultImportance,
        priority: Priority.defaultPriority,
        color: _notificationColor,
        icon: '@drawable/ic_notification',
      ),
    );

    await _local.show(
      DateTime.now().millisecondsSinceEpoch ~/ 1000,
      title,
      body,
      details,
      payload: target?.toPayload(),
    );
  }

  Future<void> _reregisterToken(String token) async {
    try {
      final prefs = await SharedPreferences.getInstance();
      if (prefs.getBool(kNotificationsPrefKey) != true) return;
      if (!await Permission.notification.isGranted) return;
      await _apiClient().registerDevice(
        RegisterDeviceRequest(token: token, platform: kDevicePlatform),
      );
    } catch (_) {
      // Obnova tokenu je best-effort.
    }
  }
}

final pushNotificationsProvider = Provider<PushNotifications>((ref) {
  final push = PushNotifications(
    navigation: ref.watch(notificationNavigationProvider),
    apiClient: () => ref.read(apiClientProvider),
  );
  ref.onDispose(push.dispose);
  return push;
});
