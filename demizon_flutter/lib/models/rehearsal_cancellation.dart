import 'package:json_annotation/json_annotation.dart';

part 'rehearsal_cancellation.g.dart';

/// Protějšek `Demizon.Contracts/Rehearsals/RehearsalCancellationDto.cs`.
///
/// Data jsou `DateOnly` (`yyyy-MM-dd`), obě včetně. Drží se jako text: kalendářní
/// den nemá pásmo a `DateTime.parse` by ho v CEST mohl posunout.
@JsonSerializable()
class RehearsalCancellation {
  const RehearsalCancellation({
    required this.id,
    required this.dateFrom,
    required this.dateTo,
    this.reason,
    this.createdBy,
  });

  final int id;
  final String dateFrom;
  final String dateTo;
  final String? reason;
  final String? createdBy;

  /// Výjimka na víc dní (prázdniny) — obnovení vrátí celé období.
  bool get isRange => dateFrom != dateTo;

  factory RehearsalCancellation.fromJson(Map<String, dynamic> json) => _$RehearsalCancellationFromJson(json);
  Map<String, dynamic> toJson() => _$RehearsalCancellationToJson(this);
}

/// Protějšek `CancelRehearsalsRequest`. `notify` pošle push členům, kteří na
/// zkoušku nemají „ne“.
@JsonSerializable()
class CancelRehearsalsRequest {
  const CancelRehearsalsRequest({required this.dateFrom, required this.dateTo, this.reason, this.notify = false});

  final String dateFrom;
  final String dateTo;
  final String? reason;
  final bool notify;

  factory CancelRehearsalsRequest.fromJson(Map<String, dynamic> json) => _$CancelRehearsalsRequestFromJson(json);
  Map<String, dynamic> toJson() => _$CancelRehearsalsRequestToJson(this);
}
