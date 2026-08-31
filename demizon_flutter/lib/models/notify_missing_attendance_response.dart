import 'package:json_annotation/json_annotation.dart';

part 'notify_missing_attendance_response.g.dart';

/// Protějšek Demizon.Contracts/Notifications/NotifyMissingAttendanceResponse.cs
///
/// Výsledek hromadného upozornění na chybějící docházku.
@JsonSerializable()
class NotifyMissingAttendanceResponse {
  const NotifyMissingAttendanceResponse({
    required this.notifiedCount,
    required this.skippedWithAttendance,
    required this.skippedWithoutNotifications,
  });

  final int notifiedCount;
  final int skippedWithAttendance;
  final int skippedWithoutNotifications;

  factory NotifyMissingAttendanceResponse.fromJson(Map<String, dynamic> json) =>
      _$NotifyMissingAttendanceResponseFromJson(json);

  Map<String, dynamic> toJson() =>
      _$NotifyMissingAttendanceResponseToJson(this);
}
