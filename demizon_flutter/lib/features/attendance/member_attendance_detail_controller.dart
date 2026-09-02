import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:demizon/core/providers.dart';
import 'package:demizon/models/models.dart';

/// Admin edituje docházku konkrétního člena — protějšek
/// `ViewModels/Attendance/MemberAttendanceDetailViewModel.cs`.
///
/// Duální režim: buď **akce** (`eventId`), nebo **zkouška** (`rehearsalDate`).
/// Zkouška není událost — nemá `id`, nese ji datum, a jde na jiné endpointy
/// (`/api/attendances/rehearsal/member/{memberId}` s parametrem `date`).

/// Co se edituje. Klíč rodiny providerů, proto hodnotová rovnost.
@immutable
class MemberAttendanceTarget {
  const MemberAttendanceTarget({
    this.eventId,
    this.rehearsalDate,
    required this.memberId,
    required this.memberName,
  });

  /// Pro router: `?eventId=12&memberId=3&memberName=Jan%20Novák`
  /// nebo `?rehearsalDate=2026-01-16&memberId=3&memberName=…`.
  /// go_router hodnoty dekóduje sám, `Uri.decodeComponent` se tu nevolá.
  factory MemberAttendanceTarget.fromQuery(Map<String, String> query) {
    return MemberAttendanceTarget(
      eventId: int.tryParse(query['eventId'] ?? ''),
      rehearsalDate: DateTime.tryParse(query['rehearsalDate'] ?? ''),
      memberId: int.tryParse(query['memberId'] ?? '') ?? 0,
      memberName: query['memberName'] ?? '',
    );
  }

  final int? eventId;
  final DateTime? rehearsalDate;
  final int memberId;
  final String memberName;

  /// MAUI: `EventId == 0 && !string.IsNullOrEmpty(RehearsalDateString)`.
  bool get isRehearsal =>
      (eventId == null || eventId == 0) && rehearsalDate != null;

  @override
  bool operator ==(Object other) =>
      other is MemberAttendanceTarget &&
      other.eventId == eventId &&
      other.rehearsalDate == rehearsalDate &&
      other.memberId == memberId &&
      other.memberName == memberName;

  @override
  int get hashCode =>
      Object.hash(eventId, rehearsalDate, memberId, memberName);
}

@immutable
class MemberAttendanceDetailState {
  const MemberAttendanceDetailState({
    required this.eventName,
    required this.eventDate,
    required this.status,
    this.comment,
    this.activityRole,
    this.isSaving = false,
  });

  /// Název akce; u zkoušky „Zkouška“ (MAUI si pro zkoušku vyráběl umělé
  /// `EventDto(0, "Zkouška", …)`, tady stačí dvě pole).
  final String eventName;
  final DateTime? eventDate;

  /// `"yes"` | `"maybe"` | `"no"`.
  final String status;
  final String? comment;

  /// Hodnota pro API (`"dancer"` | `"musician"` | `null`), ne český popisek.
  final String? activityRole;

  final bool isSaving;

  bool get isAttending => status == 'yes';

  MemberAttendanceDetailState copyWith({
    String? status,
    String? comment,
    String? activityRole,
    bool clearActivityRole = false,
    bool? isSaving,
  }) {
    return MemberAttendanceDetailState(
      eventName: eventName,
      eventDate: eventDate,
      status: status ?? this.status,
      comment: comment ?? this.comment,
      activityRole:
          clearActivityRole ? null : (activityRole ?? this.activityRole),
      isSaving: isSaving ?? this.isSaving,
    );
  }
}

final memberAttendanceDetailProvider = AsyncNotifierProvider.family<
    MemberAttendanceDetailController,
    MemberAttendanceDetailState,
    MemberAttendanceTarget>(
  MemberAttendanceDetailController.new,
);

class MemberAttendanceDetailController extends FamilyAsyncNotifier<
    MemberAttendanceDetailState, MemberAttendanceTarget> {
  @override
  Future<MemberAttendanceDetailState> build(
      MemberAttendanceTarget arg) async {
    // MAUI se v `LoadAsync` jen tiše vrátilo (`if (MemberId == 0) return;`).
    // Tady je z toho chyba — obrazovka ukáže „Nepodařilo se načíst docházku.“
    if (arg.memberId == 0 || (!arg.isRehearsal && (arg.eventId ?? 0) == 0)) {
      throw ArgumentError('Chybí memberId nebo eventId/rehearsalDate.');
    }

    final api = ref.read(apiClientProvider);

    if (arg.isRehearsal) {
      final date = arg.rehearsalDate!;
      Attendance? attendance;
      try {
        attendance = await api.getMemberRehearsalAttendance(arg.memberId, date);
      } catch (_) {
        // MAUI: chybějící docházka na zkoušku není chyba — bere se jako "no".
        attendance = null;
      }
      return MemberAttendanceDetailState(
        eventName: 'Zkouška',
        eventDate: date,
        status: attendance?.status ?? 'no',
        comment: attendance?.comment,
      );
    }

    final eventId = arg.eventId!;
    // Protějšek `Task.WhenAll` — obojí se načítá souběžně.
    final results = await Future.wait<Object>([
      api.getEvent(eventId),
      api.getMemberAttendance(eventId, arg.memberId),
    ]);
    final event = results[0] as Event;
    final attendance = results[1] as Attendance;

    return MemberAttendanceDetailState(
      eventName: event.name,
      eventDate: event.dateFrom,
      status: attendance.status,
      comment: attendance.comment,
      activityRole: attendance.activityRole,
    );
  }

  void setStatus(String value) {
    final current = state.valueOrNull;
    if (current == null) return;
    state = AsyncData(current.copyWith(status: value));
  }

  void setComment(String? value) {
    final current = state.valueOrNull;
    if (current == null) return;
    state = AsyncData(current.copyWith(comment: value));
  }

  /// [apiRole] je hodnota pro API, ne český popisek.
  void setActivityRole(String? apiRole) {
    final current = state.valueOrNull;
    if (current == null) return;
    state = AsyncData(apiRole == null
        ? current.copyWith(clearActivityRole: true)
        : current.copyWith(activityRole: apiRole));
  }

  /// Uloží docházku. `true` = uloženo (obrazovka se zavře),
  /// `false` = „Nepodařilo se uložit docházku.“
  Future<bool> save() async {
    final current = state.valueOrNull;
    if (current == null) return false;

    state = AsyncData(current.copyWith(isSaving: true));
    try {
      final api = ref.read(apiClientProvider);
      final request = UpsertAttendanceRequest(
        status: current.status,
        comment: current.comment,
        // U zkoušky se role neposílá (MAUI posílalo natvrdo null).
        activityRole: arg.isRehearsal ? null : current.activityRole,
      );

      if (arg.isRehearsal) {
        await api.upsertMemberRehearsalAttendance(
          arg.memberId,
          arg.rehearsalDate!,
          request,
        );
      } else {
        await api.upsertMemberAttendance(arg.eventId!, arg.memberId, request);
      }
      return true;
    } catch (_) {
      return false;
    } finally {
      final latest = state.valueOrNull;
      if (latest != null) {
        state = AsyncData(latest.copyWith(isSaving: false));
      }
    }
  }
}

// ─────────────────────────────────────────────────────────────────────────
// Mapa rolí CZ<->API žije v `core/formatting.dart` — používá ji i detail akce.
