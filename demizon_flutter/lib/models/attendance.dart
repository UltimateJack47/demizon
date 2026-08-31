import 'package:json_annotation/json_annotation.dart';

part 'attendance.g.dart';

/// Protějšek Demizon.Contracts/Attendances/AttendanceDto.cs
///
/// Docházka jednoho člena na akci nebo zkoušce.
/// `status` je vždy jedno z `"yes"` | `"maybe"` | `"no"`.
@JsonSerializable()
class Attendance {
  const Attendance({
    required this.id,
    required this.status,
    this.comment,
    this.activityRole,
    required this.lastUpdated,
  });

  final int id;
  final String status;
  final String? comment;
  final String? activityRole;
  final DateTime lastUpdated;

  factory Attendance.fromJson(Map<String, dynamic> json) =>
      _$AttendanceFromJson(json);

  Map<String, dynamic> toJson() => _$AttendanceToJson(this);
}
