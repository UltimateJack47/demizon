import 'package:json_annotation/json_annotation.dart';

part 'dance_document.g.dart';

/// Protějšek Demizon.Contracts/Dances/DanceDocumentDto.cs
///
/// Dokument připojený k tanci. `fileSize` je v C# `long`,
/// v Dartu stačí `int` (64bit na VM i v AOT).
@JsonSerializable()
class DanceDocument {
  const DanceDocument({
    required this.id,
    required this.fileName,
    required this.contentType,
    required this.fileSize,
  });

  final int id;
  final String fileName;
  final String contentType;
  final int fileSize;

  factory DanceDocument.fromJson(Map<String, dynamic> json) =>
      _$DanceDocumentFromJson(json);

  Map<String, dynamic> toJson() => _$DanceDocumentToJson(this);
}
