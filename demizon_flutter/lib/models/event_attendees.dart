import 'package:json_annotation/json_annotation.dart';

part 'event_attendees.g.dart';

/// Protějšek Demizon.Contracts/Events/EventAttendeesDto.cs (záznam EventAttendeeDto)
///
/// Jeden účastník akce.
@JsonSerializable()
class EventAttendee {
  const EventAttendee({
    required this.memberId,
    required this.fullName,
    this.activityRole,
  });

  final int memberId;
  final String fullName;
  final String? activityRole;

  factory EventAttendee.fromJson(Map<String, dynamic> json) =>
      _$EventAttendeeFromJson(json);

  Map<String, dynamic> toJson() => _$EventAttendeeToJson(this);
}

/// Protějšek Demizon.Contracts/Events/EventAttendeesDto.cs
///
/// Seznam účastníků akce se souhrnnými počty.
@JsonSerializable(explicitToJson: true)
class EventAttendees {
  const EventAttendees({
    required this.attendees,
    required this.dancerCount,
    required this.musicianCount,
    required this.totalCount,
  });

  final List<EventAttendee> attendees;
  final int dancerCount;
  final int musicianCount;
  final int totalCount;

  factory EventAttendees.fromJson(Map<String, dynamic> json) =>
      _$EventAttendeesFromJson(json);

  Map<String, dynamic> toJson() => _$EventAttendeesToJson(this);
}
