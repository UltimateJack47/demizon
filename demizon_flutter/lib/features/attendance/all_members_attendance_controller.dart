import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:demizon/core/providers.dart';
import 'package:demizon/models/models.dart';

import 'attendance_controller.dart' show YearMonth;

/// Přehled docházky všech členů za měsíc — protějšek
/// `ViewModels/Attendance/AllMembersAttendanceViewModel.cs`.
///
/// Kromě tabulky nese stav i to, **kdo se dívá**: podle toho se u buňky
/// rozhoduje, jestli tap otevře můj detail akce, cizí docházku (admin),
/// nebo jen poznámku.

@immutable
class AllMembersAttendanceState {
  const AllMembersAttendanceState({
    required this.table,
    required this.currentMemberId,
    required this.isAdmin,
  });

  final MonthlyAttendanceTable table;

  /// `memberId` přihlášeného uživatele; `null`, pokud ho úložiště nezná.
  final int? currentMemberId;

  final bool isAdmin;

  /// Protějšek `AllMembersAttendanceViewModel.IsCurrentUser`.
  bool isCurrentUser(int memberId) =>
      currentMemberId != null && currentMemberId == memberId;

  /// Protějšek `HasData`.
  bool get hasData => table.members.isNotEmpty;
}

final allMembersAttendanceProvider = AsyncNotifierProvider.family<
    AllMembersAttendanceController, AllMembersAttendanceState, YearMonth>(
  AllMembersAttendanceController.new,
);

class AllMembersAttendanceController
    extends FamilyAsyncNotifier<AllMembersAttendanceState, YearMonth> {
  @override
  Future<AllMembersAttendanceState> build(YearMonth arg) => _load();

  Future<AllMembersAttendanceState> _load() async {
    final api = ref.read(apiClientProvider);

    // TODO(verify): očekávaný kontrakt `core/providers.dart` —
    // `tokenStorageProvider` vrací `TokenStorage` z `core/auth/token_storage.dart`
    // (getter `memberId`, getter `role`). Pokud session bude držet
    // `authControllerProvider`, přečti identitu odtamtud.
    final storage = ref.read(tokenStorageProvider);

    final memberId = await storage.memberId;
    final role = await storage.role;
    final table = await api.getMonthlyAttendanceTable(arg.year, arg.month);

    return AllMembersAttendanceState(
      table: table,
      currentMemberId: memberId,
      // MAUI: string.Equals(role, "Admin", OrdinalIgnoreCase)
      isAdmin: role?.toLowerCase() == 'admin',
    );
  }

  Future<void> refresh() async {
    state = await AsyncValue.guard(_load);
  }
}
