import 'package:json_annotation/json_annotation.dart';

part 'member_cell.g.dart';

/// Protějšek Demizon.Contracts/Attendances/MemberCellDto.cs
///
/// Jedna buňka (člen × sloupec) v měsíční tabulce docházky.
/// `eventId == null` znamená zkoušku, ne akci.
@JsonSerializable()
class MemberCell {
  const MemberCell({
    required this.date,
    this.eventId,
    this.status,
    this.comment,
  });

  final DateTime date;
  final int? eventId;
  final String? status;
  final String? comment;

  factory MemberCell.fromJson(Map<String, dynamic> json) =>
      _$MemberCellFromJson(json);

  Map<String, dynamic> toJson() => _$MemberCellToJson(this);
}
