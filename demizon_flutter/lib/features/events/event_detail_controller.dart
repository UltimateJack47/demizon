import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:demizon/core/formatting.dart';
import 'package:demizon/core/invalidation.dart';
import 'package:demizon/core/providers.dart';
import 'package:demizon/models/models.dart';

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
/// V režimu zkoušky se `Event` bere z `/api/events/month` (skutečný čas,
/// zrušení a jeho důvod). MAUI si ho skládal lokálně a zrušenou zkoušku by
/// ukázal jako normální; lokální složení zůstává jen jako záloha, když
/// volání selže. Seznam účastníků se u zkoušky nezobrazuje vůbec.
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

  /// Složení z routeru: `/events/0?rehearsalDate=…` má `eventId == 0`,
  /// ale režim zkoušky pozná až vyplněné datum — stejně jako
  /// `EventDetailViewModel.IsRehearsal`.
  factory EventDetailArgs.fromRoute({int? eventId, DateTime? rehearsalDate}) {
    if (rehearsalDate != null && (eventId == null || eventId == 0)) {
      return EventDetailArgs(rehearsalDate: rehearsalDate);
    }
    return EventDetailArgs(eventId: eventId);
  }

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

  /// Zrušená zkouška (nebo den, kdy žádná není) se vyplnit nedá — server by
  /// uložení odmítl s 400.
  bool get isLocked => isRehearsal && (event?.isCancelled ?? false);

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

/// Zkouška z odpovědi `/api/events/month`. `null` = v ten den žádná není
/// (připadla na ni akce, nebo to není den zkoušky).
Event? findRehearsal(List<Event> month, DateTime date) {
  for (final e in month) {
    if (e.isRehearsal &&
        e.dateFrom.year == date.year &&
        e.dateFrom.month == date.month &&
        e.dateFrom.day == date.day) {
      return e;
    }
  }
  return null;
}

/// Záloha, když se stav zkoušky nepodařilo načíst. Časy podle pravidla
/// na serveru (19:00–21:30), aby hlavička neukazovala půlnoc.
Event rehearsalPlaceholder(DateTime date) {
  final start = DateTime(date.year, date.month, date.day, 19);
  return Event(
    id: 0,
    name: 'Zkouška',
    dateFrom: start,
    dateTo: start.add(const Duration(hours: 2, minutes: 30)),
    place: null,
    isCancelled: false,
    recurrence: 'none',
    isRehearsal: true,
  );
}

/// Den, kdy podle serveru zkouška není — typicky na pátek mezitím
/// připadla akce. Zobrazí se jako zrušená, aby nešla vyplnit.
Event noRehearsalOn(DateTime date) {
  final placeholder = rehearsalPlaceholder(date);
  return Event(
    id: 0,
    name: placeholder.name,
    dateFrom: placeholder.dateFrom,
    dateTo: placeholder.dateTo,
    place: null,
    isCancelled: true,
    recurrence: placeholder.recurrence,
    isRehearsal: true,
    cancellationReason: 'V tento den zkouška není.',
  );
}

/// `autoDispose`: detail se při každém otevření načte znovu, jinak by v cache
/// zůstala chyba načtení i docházka, kterou mezitím změnil admin.
final eventDetailProvider = AsyncNotifierProvider.autoDispose
    .family<EventDetailController, EventDetailState, EventDetailArgs>(
  EventDetailController.new,
);

class EventDetailController
    extends AutoDisposeFamilyAsyncNotifier<EventDetailState, EventDetailArgs> {
  @override
  Future<EventDetailState> build(EventDetailArgs arg) async {
    final api = ref.read(apiClientProvider);

    // `TokenStorage.role` je Future<String?> (protějšek GetRoleAsync).
    final role = await ref.read(tokenStorageProvider).role;
    final isAdmin = role?.toLowerCase() == 'admin';

    if (arg.isRehearsal) {
      final date = arg.rehearsalDate!;
      Event event;
      try {
        final month = await api.getEventsByMonth(date.year, date.month);
        event = findRehearsal(month, date) ?? noRehearsalOn(date);
      } catch (e) {
        // Bez spojení se stav zjistit nedá. Formulář necháme odemčený,
        // server případné zrušení stejně pohlídá.
        debugPrint('Stav zkoušky ${encodeQueryValue(date)} nezjištěn: $e');
        event = rehearsalPlaceholder(date);
      }

      // Docházka na zkoušku nemusí existovat — 404 znamená "nepřijdu".
      // Jiná chyba propadne do chybové obrazovky: jako "nepřijdu" by ji člen
      // uložením přepsal přes svou skutečnou odpověď.
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
      } on DioException catch (e) {
        if (e.response?.statusCode != 404) rethrow;
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
    } catch (e) {
      debugPrint('Seznam účastníků akce $eventId se nenačetl: $e');
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
    invalidateAttendanceViews(ref.invalidate);
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
    invalidateAttendanceViews(ref.invalidate);
  }
}
