import 'package:json_annotation/json_annotation.dart';

part 'event_requests.g.dart';

/// Protějšek Demizon.Contracts/Events/CreateEventRequest.cs
///
/// Založení nové akce.
@JsonSerializable()
class CreateEventRequest {
  const CreateEventRequest({
    required this.name,
    required this.dateFrom,
    required this.dateTo,
    this.place,
    required this.recurrence,
  });

  final String name;
  final DateTime dateFrom;
  final DateTime dateTo;
  final String? place;
  final String recurrence;

  factory CreateEventRequest.fromJson(Map<String, dynamic> json) =>
      _$CreateEventRequestFromJson(json);

  Map<String, dynamic> toJson() => _$CreateEventRequestToJson(this);
}

/// Protějšek Demizon.Contracts/Events/UpdateEventRequest.cs
///
/// Úprava akce. Oproti CreateEventRequest navíc `isPublic` a `isCancelled`.
@JsonSerializable()
class UpdateEventRequest {
  const UpdateEventRequest({
    required this.name,
    required this.dateFrom,
    required this.dateTo,
    this.place,
    required this.recurrence,
    required this.isPublic,
    required this.isCancelled,
  });

  final String name;
  final DateTime dateFrom;
  final DateTime dateTo;
  final String? place;
  final String recurrence;
  final bool isPublic;
  final bool isCancelled;

  factory UpdateEventRequest.fromJson(Map<String, dynamic> json) =>
      _$UpdateEventRequestFromJson(json);

  Map<String, dynamic> toJson() => _$UpdateEventRequestToJson(this);
}
