import 'package:json_annotation/json_annotation.dart';

part 'gallery_photo.g.dart';

/// Protějšek Demizon.Contracts/Gallery/GalleryPhotoDto.cs
///
/// Fotka v galerii. Samotný obrázek se nestahuje přes API klienta —
/// je na `{baseUrl}/api/files/{id}/image?size=full|thumb`.
@JsonSerializable()
class GalleryPhoto {
  const GalleryPhoto({
    required this.id,
    this.danceName,
  });

  final int id;
  final String? danceName;

  factory GalleryPhoto.fromJson(Map<String, dynamic> json) =>
      _$GalleryPhotoFromJson(json);

  Map<String, dynamic> toJson() => _$GalleryPhotoToJson(this);
}
