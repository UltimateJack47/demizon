import 'package:json_annotation/json_annotation.dart';

part 'member_attendance_stat.g.dart';

/// Protějšek Demizon.Contracts/Attendances/MemberAttendanceStatDto.cs
///
/// Statistika docházky jednoho člena — zvlášť zkoušky, zvlášť akce.
@JsonSerializable()
class MemberAttendanceStat {
  const MemberAttendanceStat({
    required this.memberId,
    required this.fullName,
    required this.totalRehearsals,
    required this.attendedRehearsals,
    required this.rehearsalRate,
    required this.totalActions,
    required this.attendedActions,
    required this.actionRate,
  });

  final int memberId;
  final String fullName;
  final int totalRehearsals;
  final int attendedRehearsals;
  final double rehearsalRate;
  final int totalActions;
  final int attendedActions;
  final double actionRate;

  factory MemberAttendanceStat.fromJson(Map<String, dynamic> json) =>
      _$MemberAttendanceStatFromJson(json);

  Map<String, dynamic> toJson() => _$MemberAttendanceStatToJson(this);
}
