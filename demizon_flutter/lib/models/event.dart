import 'package:json_annotation/json_annotation.dart';

import 'attendance.dart';

part 'event.g.dart';

/// Protějšek Demizon.Contracts/Events/EventDto.cs
///
/// Akce souboru. Pozor: zkoušky nejsou samostatné události — server je
/// posílá jako EventDto s `isRehearsal == true`.
@JsonSerializable(explicitToJson: true)
class Event {
  const Event({
    required this.id,
    required this.name,
    required this.dateFrom,
    required this.dateTo,
    this.place,
    required this.isCancelled,
    required this.recurrence,
    this.myAttendance,
    this.isRehearsal = false,
    this.isPublic = false,
    this.notifyBeforeDays,
  });

  final int id;
  final String name;
  final DateTime dateFrom;
  final DateTime dateTo;
  final String? place;
  final bool isCancelled;
  final String recurrence;
  final Attendance? myAttendance;
  final bool isRehearsal;
  final bool isPublic;
  final int? notifyBeforeDays;

  factory Event.fromJson(Map<String, dynamic> json) => _$EventFromJson(json);

  Map<String, dynamic> toJson() => _$EventToJson(this);
}
