import 'package:json_annotation/json_annotation.dart';

part 'create_video_link_request.g.dart';

/// Protějšek Demizon.Contracts/Dances/CreateVideoLinkRequest.cs
///
/// Vytvoření odkazu na video. Na rozdíl od VideoLinkDto nemá server
/// pro `isVisible` / `isInternal` výchozí hodnoty — posílají se vždy.
@JsonSerializable()
class CreateVideoLinkRequest {
  const CreateVideoLinkRequest({
    required this.name,
    required this.url,
    required this.year,
    required this.isVisible,
    required this.isInternal,
    this.danceId,
  });

  final String name;
  final String url;
  final int year;
  final bool isVisible;
  final bool isInternal;
  final int? danceId;

  factory CreateVideoLinkRequest.fromJson(Map<String, dynamic> json) =>
      _$CreateVideoLinkRequestFromJson(json);

  Map<String, dynamic> toJson() => _$CreateVideoLinkRequestToJson(this);
}
