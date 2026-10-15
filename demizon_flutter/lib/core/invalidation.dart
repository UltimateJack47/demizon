import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../features/attendance/all_members_attendance_controller.dart';
import '../features/attendance/attendance_controller.dart';
import '../features/attendance/attendance_stats_controller.dart';
import '../features/dances/dances_controller.dart';
import '../features/events/events_controller.dart';
import '../features/gallery/gallery_controller.dart';
import '../features/profile/profile_controller.dart';

/// `ref.invalidate` — stejná signatura na `Ref` i `WidgetRef`, takže se dá
/// předat tear-off z controlleru i z obrazovky.
typedef Invalidate = void Function(ProviderOrFamily provider);

/// Všechno, co ukazuje docházku nebo seznam akcí. Volá se po každé změně
/// docházky či akce — protějšek MAUI `EventsChangedMessage` + `OnAppearing`.
///
/// Rodiny se invalidují celé: stejná akce je vidět ve víc měsících/obdobích
/// a neposlouchané instance se znovu načtou až při dalším otevření.
void invalidateAttendanceViews(Invalidate invalidate) {
  invalidate(eventsProvider);
  invalidate(attendanceProvider);
  invalidate(allMembersAttendanceProvider);
  invalidate(attendanceStatsProvider);
}

/// Data vázaná na přihlášeného člena. Volá se při přihlášení, ne při
/// odhlášení: v tu chvíli jsou obrazovky ještě připojené a invalidace by
/// spustila requesty bez tokenu. Při přihlášení nic poslouchané není, takže
/// se jen označí jako zastaralé a načtou se až pod novým účtem.
void invalidateSessionData(Invalidate invalidate) {
  invalidateAttendanceViews(invalidate);
  invalidate(dancesProvider);
  invalidate(galleryProvider);
  invalidate(profileProvider);
}
