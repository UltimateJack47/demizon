import 'dart:convert';

import '../routes.dart';

/// Cíl deep-linku z notifikace — protějšek `PendingTarget`
/// v `NotificationNavigationService.cs`.
///
/// Payload z FCM nese jeden z klíčů: `eventId` (akce), `rehearsalDate` (zkouška
/// ve tvaru `yyyy-MM-dd`), `costumeTransferId` (předávka krojů) nebo
/// `costumes` = `mine` (připomínka vrácení). Klíče posílá `AttendanceReminderService`
/// a `CostumeNotifications` na serveru.
class NotificationTarget {
  const NotificationTarget._({this.eventId, this.rehearsalDate, this.costumeTransferId, this.myCostumes = false});

  final int? eventId;

  /// Kalendářní den zkoušky, `yyyy-MM-dd`.
  final String? rehearsalDate;

  final int? costumeTransferId;

  final bool myCostumes;

  /// Cesta pro go_router. Zkouška nemá id, proto `/events/0?rehearsalDate=…`
  /// — stejný tvar jako `rehearsalDetailPath`.
  String get location {
    if (eventId != null) return AppRoutes.eventDetailFor(eventId!);
    if (rehearsalDate != null) return '${AppRoutes.eventDetailFor(0)}?rehearsalDate=$rehearsalDate';
    if (costumeTransferId != null) return AppRoutes.costumeTransferFor(costumeTransferId!);
    return AppRoutes.myCostumes;
  }

  String toPayload() {
    if (eventId != null) return jsonEncode({'eventId': '$eventId'});
    if (rehearsalDate != null) return jsonEncode({'rehearsalDate': rehearsalDate});
    if (costumeTransferId != null) return jsonEncode({'costumeTransferId': '$costumeTransferId'});
    return jsonEncode({'costumes': 'mine'});
  }

  /// MAUI: `eventId` má přednost, jinak `rehearsalDate`; krojárka je až za nimi.
  /// Prázdný / neparsovatelný payload se tiše zahodí — notifikace bez cíle jen
  /// otevře aplikaci.
  static NotificationTarget? parse(Map<String, dynamic>? data) {
    if (data == null) return null;

    final eventId = _positiveInt(data['eventId']);
    if (eventId != null) return NotificationTarget._(eventId: eventId);

    final dateRaw = data['rehearsalDate']?.toString().trim();
    if (dateRaw != null && dateRaw.isNotEmpty) {
      return NotificationTarget._(rehearsalDate: dateRaw);
    }

    final transferId = _positiveInt(data['costumeTransferId']);
    if (transferId != null) return NotificationTarget._(costumeTransferId: transferId);

    if (data['costumes']?.toString() == 'mine') return const NotificationTarget._(myCostumes: true);

    return null;
  }

  static int? _positiveInt(Object? raw) {
    final id = int.tryParse(raw?.toString() ?? '');
    return id != null && id > 0 ? id : null;
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
      other.rehearsalDate == rehearsalDate &&
      other.costumeTransferId == costumeTransferId &&
      other.myCostumes == myCostumes;

  @override
  int get hashCode => Object.hash(eventId, rehearsalDate, costumeTransferId, myCostumes);
}
