import 'package:json_annotation/json_annotation.dart';

part 'monthly_column.g.dart';

/// Protějšek Demizon.Contracts/Attendances/MonthlyColumnDto.cs
///
/// Jeden sloupec měsíční tabulky docházky — buď páteční zkouška
/// (`isEvent == false`, `eventId == null`), nebo pojmenovaná akce.
@JsonSerializable()
class MonthlyColumn {
  const MonthlyColumn({
    this.eventId,
    required this.label,
    required this.date,
    required this.isEvent,
    required this.isCancelled,
  });

  final int? eventId;
  final String label;
  final DateTime date;
  final bool isEvent;
  final bool isCancelled;

  factory MonthlyColumn.fromJson(Map<String, dynamic> json) =>
      _$MonthlyColumnFromJson(json);

  Map<String, dynamic> toJson() => _$MonthlyColumnToJson(this);
}
