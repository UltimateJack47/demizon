import 'package:json_annotation/json_annotation.dart';

part 'video_link.g.dart';

/// Protějšek Demizon.Contracts/Dances/VideoLinkDto.cs
///
/// Odkaz na video u tance. `isInternal` znamená, že video je jen pro členy.
@JsonSerializable()
class VideoLink {
  const VideoLink({
    required this.id,
    required this.name,
    required this.url,
    required this.year,
    this.isVisible = true,
    this.isInternal = false,
  });

  final int id;
  final String name;
  final String url;
  final int year;
  final bool isVisible;
  final bool isInternal;

  factory VideoLink.fromJson(Map<String, dynamic> json) =>
      _$VideoLinkFromJson(json);

  Map<String, dynamic> toJson() => _$VideoLinkToJson(this);
}
