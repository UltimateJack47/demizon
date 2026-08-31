import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:demizon/core/formatting.dart';
import 'package:demizon/core/providers.dart';
import 'package:demizon/models/models.dart';

import 'events_controller.dart';

/// Přepis `Demizon.Maui/ViewModels/EventDetailViewModel.cs`.
///
/// **Duální režim (EventDetailViewModel.cs:22).** Obrazovka slouží dvěma
/// věcem najednou:
///
/// * **akce** — `eventId != null`, docházka jde na `/api/attendances/{eventId}`
/// * **zkouška** — `eventId == null`, `rehearsalDate != null`; zkouška není
///   událost, je to řádek docházky bez `eventId`, takže má vlastní čtveřici
///   endpointů s parametrem `date`:
///     GET    /api/attendances/rehearsal?date=…                      getRehearsalAttendance
///     PUT    /api/attendances/rehearsal?date=…                      upsertRehearsalAttendance
///     DELETE /api/attendances/rehearsal?date=…                      deleteMyRehearsalAttendance
///     POST   /api/events/rehearsals/notify-missing-attendance?date=… notifyMissingRehearsalAttendance
///
/// V režimu zkoušky se `Event` nenačítá z API — MAUI si ho složil lokálně
/// ("Zkouška", dvě hodiny, `isRehearsal: true`) a seznam účastníků se
/// nezobrazuje vůbec.
@immutable
class EventDetailArgs {
  const EventDetailArgs({this.eventId, this.rehearsalDate})
      : assert(
          eventId != null || rehearsalDate != null,
          'Musí být zadané buď eventId (akce), nebo rehearsalDate (zkouška).',
        );

  final int? eventId;

  /// Datum zkoušky (bez času). MAUI ho vozil jako string "yyyy-MM-dd".
  final DateTime? rehearsalDate;

  bool get isRehearsal => eventId == null && rehearsalDate != null;

  @override
  bool operator ==(Object other) =>
      other is EventDetailArgs &&
      other.eventId == eventId &&
      other.rehearsalDate == rehearsalDate;

  @override
  int get hashCode => Object.hash(eventId, rehearsalDate);
}

const Object _unset = Object();

@immutable
class EventDetailState {
  const EventDetailState({
    required this.args,
    this.event,
    this.attendees,
    this.status = 'no',
    this.comment,
    this.activityRole,
    this.isAdmin = false,
  });

  final EventDetailArgs args;
  final Event? event;
  final EventAttendees? attendees;

  /// "yes" | "maybe" | "no" | "" (prázdné = reset, docházka se smaže).
  final String status;
  final String? comment;

  /// Český popisek role pro výběr ("Tanečník" / "Muzikant").
  /// Na API hodnotu ("dancer" / "musician") se převádí až při ukládání.
  final String? activityRole;
  final bool isAdmin;

  bool get isRehearsal => args.isRehearsal;
  bool get isAttending => status == 'yes';
  bool get hasStatus => status.isNotEmpty;
  bool get showAttendees => !isRehearsal && (attendees?.totalCount ?? 0) > 0;

  /// `EventDetailViewModel.CanSendReminder` — admin, akce není zrušená
  /// a začíná až po dnešku.
  bool get canSendReminder {
    final ev = event;
    if (!isAdmin || ev == null || ev.isCancelled) return false;
    final today = DateTime.now();
    final from = DateTime(ev.dateFrom.year, ev.dateFrom.month, ev.dateFrom.day);
    return from.isAfter(DateTime(today.year, today.month, today.day));
  }

  EventDetailState copyWith({
    Event? event,
    Object? attendees = _unset,
    String? status,
    Object? comment = _unset,
    Object? activityRole = _unset,
    bool? isAdmin,
  }) {
    return EventDetailState(
      args: args,
      event: event ?? this.event,
      attendees:
          attendees == _unset ? this.attendees : attendees as EventAttendees?,
      status: status ?? this.status,
      comment: comment == _unset ? this.comment : comment as String?,
      activityRole: activityRole == _unset
          ? this.activityRole
          : activityRole as String?,
      isAdmin: isAdmin ?? this.isAdmin,
    );
  }
}

final eventDetailProvider = AsyncNotifierProvider.family<EventDetailController,
    EventDetailState, EventDetailArgs>(EventDetailController.new);

class EventDetailController
    extends FamilyAsyncNotifier<EventDetailState, EventDetailArgs> {
  @override
  Future<EventDetailState> build(EventDetailArgs arg) async {
    final api = ref.read(apiClientProvider);

    // TODO(verify): očekávaný kontrakt `TokenStorage.getRole()` →
    // Future<String?> (protějšek TokenStorage.GetRoleAsync).
    final role = await ref.read(tokenStorageProvider).getRole();
    final isAdmin = role?.toLowerCase() == 'admin';

    if (arg.isRehearsal) {
      final date = arg.rehearsalDate!;
      // TODO(verify): pojmenované parametry konstruktoru `Event` podle
      // lib/models/event.dart (protějšek EventDto).
      final event = Event(
        id: 0,
        name: 'Zkouška',
        dateFrom: date,
        dateTo: date.add(const Duration(hours: 2)),
        place: null,
        isCancelled: false,
        recurrence: 'Weekly',
        isRehearsal: true,
      );

      // Docházka na zkoušku nemusí existovat — 404 znamená "nepřijdu".
      try {
        final att = await api.getRehearsalAttendance(date);
        return EventDetailState(
          args: arg,
          event: event,
          isAdmin: isAdmin,
          status: att.status,
          comment: att.comment,
          activityRole: apiRoleToDisplay(att.activityRole),
        );
      } catch (_) {
        return EventDetailState(
          args: arg,
          event: event,
          isAdmin: isAdmin,
          status: 'no',
        );
      }
    }

    final eventId = arg.eventId!;
    final event = await api.getEvent(eventId);

    // Seznam "kdo přijde" je doplněk — když selže, detail se zobrazí bez něj.
    EventAttendees? attendees;
    try {
      attendees = await api.getEventAttendees(eventId);
    } catch (_) {
      attendees = null;
    }

    final att = event.myAttendance;
    return EventDetailState(
      args: arg,
      event: event,
      attendees: attendees,
      isAdmin: isAdmin,
      status: att?.status ?? 'no',
      comment: att?.comment,
      activityRole: apiRoleToDisplay(att?.activityRole),
    );
  }

  EventDetailState get _value => state.requireValue;

  /// Tlačítka ✓ / ? / ✗ / ↺ — prázdná hodnota znamená reset docházky.
  void setStatus(String value) {
    state = AsyncData(_value.copyWith(status: value));
  }

  void setActivityRole(String? displayRole) {
    state = AsyncData(_value.copyWith(activityRole: displayRole));
  }

  void setComment(String? comment) {
    state = AsyncData(_value.copyWith(comment: comment));
  }

  /// `SaveAttendanceAsync`. Prázdný status = smazání záznamu docházky.
  /// Chybu propaguje volajícímu (obrazovka ji ukáže v dialogu).
  Future<void> saveAttendance({String? comment}) async {
    final value = _value;
    final api = ref.read(apiClientProvider);
    final text = comment ?? value.comment;

    if (value.status.isEmpty) {
      if (value.isRehearsal) {
        await api.deleteMyRehearsalAttendance(value.args.rehearsalDate!);
      } else {
        await api.deleteMyAttendance(value.args.eventId!);
      }
    } else {
      final request = UpsertAttendanceRequest(
        status: value.status,
        comment: text,
        activityRole: displayRoleToApi(value.activityRole),
      );
      if (value.isRehearsal) {
        await api.upsertRehearsalAttendance(
          value.args.rehearsalDate!,
          request,
        );
      } else {
        await api.upsertAttendance(value.args.eventId!, request);
      }
    }

    // Protějšek WeakReferenceMessenger.Send(new EventsChangedMessage()).
    ref.invalidate(eventsProvider);
  }

  /// `SendReminderAsync`. Vrací hlášku, kterou obrazovka zobrazí v dialogu
  /// "Hotovo" — složení textu je 1:1 z MAUI.
  Future<String> sendReminder() async {
    final value = _value;
    final api = ref.read(apiClientProvider);

    final result = value.isRehearsal
        ? await api.notifyMissingRehearsalAttendance(value.args.rehearsalDate!)
        : await api.notifyMissingAttendance(value.args.eventId!);

    final parts = <String>[];
    if (result.notifiedCount > 0) {
      parts.add('Notifikace odeslána ${result.notifiedCount} členům.');
    }
    if (result.skippedWithAttendance > 0) {
      parts.add('${result.skippedWithAttendance} už docházku má vyplněnou.');
    }
    if (result.skippedWithoutNotifications > 0) {
      parts.add(
        '${result.skippedWithoutNotifications} nemá povolené notifikace.',
      );
    }

    if (result.notifiedCount == 0 && result.skippedWithoutNotifications == 0) {
      return 'Všichni (${result.skippedWithAttendance}) už docházku mají vyplněnou.';
    }
    return parts.join(' ');
  }

  /// `DeleteEventAsync`. Potvrzení řeší obrazovka.
  Future<void> deleteEvent() async {
    await ref.read(apiClientProvider).deleteEvent(_value.args.eventId!);
    ref.invalidate(eventsProvider);
  }
}
