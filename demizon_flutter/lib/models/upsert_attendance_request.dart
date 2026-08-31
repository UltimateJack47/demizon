import 'package:json_annotation/json_annotation.dart';

part 'upsert_attendance_request.g.dart';

/// Protějšek Demizon.Contracts/Attendances/UpsertAttendanceRequest.cs
///
/// Vytvoření nebo úprava docházky. `status` je `"yes"` | `"maybe"` | `"no"`.
@JsonSerializable()
class UpsertAttendanceRequest {
  const UpsertAttendanceRequest({
    required this.status,
    this.comment,
    this.activityRole,
  });

  final String status;
  final String? comment;
  final String? activityRole;

  factory UpsertAttendanceRequest.fromJson(Map<String, dynamic> json) =>
      _$UpsertAttendanceRequestFromJson(json);

  Map<String, dynamic> toJson() => _$UpsertAttendanceRequestToJson(this);
}
