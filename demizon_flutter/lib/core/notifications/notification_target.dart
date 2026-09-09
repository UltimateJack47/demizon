import 'dart:convert';

import '../routes.dart';

/// Cíl deep-linku z notifikace — protějšek `PendingTarget`
/// v `NotificationNavigationService.cs`.
///
/// Payload z FCM nese buď `eventId` (akce), nebo `rehearsalDate` (zkouška
/// ve tvaru `yyyy-MM-dd`). Stejné klíče posílá `FcmService` / `AttendanceReminderService`.
class NotificationTarget {
  const NotificationTarget._({this.eventId, this.rehearsalDate});

  final int? eventId;

  /// Kalendářní den zkoušky, `yyyy-MM-dd`.
  final String? rehearsalDate;

  /// Cesta pro go_router. Zkouška nemá id, proto `/events/0?rehearsalDate=…`
  /// — stejný tvar jako `rehearsalDetailPath`.
  String get location => eventId != null
      ? AppRoutes.eventDetailFor(eventId!)
      : '${AppRoutes.eventDetailFor(0)}?rehearsalDate=$rehearsalDate';

  String toPayload() {
    if (eventId != null) {
      return jsonEncode({'eventId': '$eventId'});
    }
    return jsonEncode({'rehearsalDate': rehearsalDate});
  }

  /// MAUI: `eventId` má přednost, jinak `rehearsalDate`. Prázdný / neparsovatelný
  /// payload se tiše zahodí — notifikace bez cíle jen otevře aplikaci.
  static NotificationTarget? parse(Map<String, dynamic>? data) {
    if (data == null) return null;

    final eventRaw = data['eventId']?.toString();
    if (eventRaw != null && eventRaw.isNotEmpty) {
      final id = int.tryParse(eventRaw);
      if (id != null && id > 0) {
        return NotificationTarget._(eventId: id);
      }
    }

    final dateRaw = data['rehearsalDate']?.toString().trim();
    if (dateRaw != null && dateRaw.isNotEmpty) {
      return NotificationTarget._(rehearsalDate: dateRaw);
    }

    return null;
  }

  static NotificationTarget? fromPayload(String? payload) {
    if (payload == null || payload.isEmpty) return null;
    try {
      final decoded = jsonDecode(payload);
      if (decoded is Map) {
        return parse(
          decoded.map((key, value) => MapEntry(key.toString(), value)),
        );
      }
    } catch (_) {
      // Poškozený payload — otevři aplikaci bez navigace, stejně jako MAUI.
    }
    return null;
  }

  @override
  bool operator ==(Object other) =>
      other is NotificationTarget &&
      other.eventId == eventId &&
      other.rehearsalDate == rehearsalDate;

  @override
  int get hashCode => Object.hash(eventId, rehearsalDate);
}
