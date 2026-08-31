import 'package:json_annotation/json_annotation.dart';

import 'video_link.dart';

part 'dance.g.dart';

/// Protějšek Demizon.Contracts/Dances/DanceDto.cs
///
/// Tanec včetně připojených videí. `internalDescription` vidí jen členové.
@JsonSerializable(explicitToJson: true)
class Dance {
  const Dance({
    required this.id,
    required this.name,
    this.region,
    this.description,
    this.internalDescription,
    this.lyrics,
    required this.videos,
  });

  final int id;
  final String name;
  final String? region;
  final String? description;
  final String? internalDescription;
  final String? lyrics;
  final List<VideoLink> videos;

  factory Dance.fromJson(Map<String, dynamic> json) => _$DanceFromJson(json);

  Map<String, dynamic> toJson() => _$DanceToJson(this);
}
