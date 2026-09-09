import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'notification_target.dart';

/// Deep-link z ťuknuté notifikace — přepis `NotificationNavigationService.cs`.
///
/// Tři cesty, stejně jako v MAUI:
///   1. Cold start  — cíl se uloží jako pending a přehraje se po přihlášení,
///      až je Shell připravený (`markReadyAndFlush`).
///   2. Background  — FCM `onMessageOpenedApp` / `getInitialMessage`.
///   3. Foreground  — tap na lokální notifikaci (`flutter_local_notifications`).
///
/// Bez Firebase i bez GoRouteru, ať jde logika otestovat bez telefonu.
class NotificationNavigation {
  void Function(String location)? _go;
  NotificationTarget? _pending;
  bool _ready = false;

  /// Připojí navigaci. Volat z `DemizonApp.build`, kde už existuje `GoRouter`.
  void bind(void Function(String location) go) => _go = go;

  void handle(Map<String, dynamic>? data) {
    final target = NotificationTarget.parse(data);
    if (target == null) return;
    _dispatchOrPend(target);
  }

  void handlePayload(String? payload) {
    final target = NotificationTarget.fromPayload(payload);
    if (target == null) return;
    _dispatchOrPend(target);
  }

  /// Po přihlášení / auto-loginu, když je MainShell na stromě.
  /// Protějšek `MarkReadyAndFlush` z `App.xaml.cs` a `LoginViewModel`.
  void markReadyAndFlush() {
    final pending = _pending;
    _ready = true;
    _pending = null;
    if (pending != null) _navigate(pending);
  }

  /// Odhlášení / vypršení session — ať se po dalším loginu neotevře starý cíl.
  void reset() {
    _ready = false;
    _pending = null;
  }

  void _dispatchOrPend(NotificationTarget target) {
    if (_ready) {
      _navigate(target);
    } else {
      _pending = target;
    }
  }

  void _navigate(NotificationTarget target) {
    final go = _go;
    if (go == null) {
      _pending = target;
      return;
    }
    go(target.location);
  }
}

final notificationNavigationProvider = Provider<NotificationNavigation>(
  (ref) => NotificationNavigation(),
);
