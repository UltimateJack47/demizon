import 'package:json_annotation/json_annotation.dart';

import 'member_monthly_row.dart';
import 'monthly_column.dart';

part 'monthly_attendance_table.g.dart';

/// Protějšek Demizon.Contracts/Attendances/MonthlyAttendanceTableDto.cs
///
/// Celá měsíční tabulka docházky: hlavičky sloupců + řádky členů.
@JsonSerializable(explicitToJson: true)
class MonthlyAttendanceTable {
  const MonthlyAttendanceTable({
    required this.columns,
    required this.members,
  });

  final List<MonthlyColumn> columns;
  final List<MemberMonthlyRow> members;

  factory MonthlyAttendanceTable.fromJson(Map<String, dynamic> json) =>
      _$MonthlyAttendanceTableFromJson(json);

  Map<String, dynamic> toJson() => _$MonthlyAttendanceTableToJson(this);
}
