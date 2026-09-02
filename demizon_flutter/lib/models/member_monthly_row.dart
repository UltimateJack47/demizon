import 'package:json_annotation/json_annotation.dart';

import 'member_cell.dart';

part 'member_monthly_row.g.dart';

/// Protějšek Demizon.Contracts/Attendances/MemberMonthlyRowDto.cs
///
/// Jeden řádek (člen) v měsíční tabulce docházky.
@JsonSerializable(explicitToJson: true)
class MemberMonthlyRow {
  const MemberMonthlyRow({
    required this.memberId,
    required this.fullName,
    required this.cells,
  });

  final int memberId;
  final String fullName;
  final List<MemberCell> cells;

  factory MemberMonthlyRow.fromJson(Map<String, dynamic> json) =>
      _$MemberMonthlyRowFromJson(json);

  Map<String, dynamic> toJson() => _$MemberMonthlyRowToJson(this);
}
