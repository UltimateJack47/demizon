import 'package:json_annotation/json_annotation.dart';

part 'member_profile.g.dart';

/// Protějšek Demizon.Contracts/Members/MemberProfileDto.cs
///
/// Profil přihlášeného člena.
@JsonSerializable()
class MemberProfile {
  const MemberProfile({
    required this.id,
    required this.name,
    required this.surname,
    required this.login,
    this.email,
    this.roles = const [],
    required this.gender,
  });

  final int id;
  final String name;
  final String surname;
  final String login;
  final String? email;
  @JsonKey(defaultValue: <String>[])
  final List<String> roles;
  final String gender;

  factory MemberProfile.fromJson(Map<String, dynamic> json) =>
      _$MemberProfileFromJson(json);

  Map<String, dynamic> toJson() => _$MemberProfileToJson(this);
}
