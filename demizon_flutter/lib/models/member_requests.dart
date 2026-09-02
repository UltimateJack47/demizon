import 'package:json_annotation/json_annotation.dart';

part 'member_requests.g.dart';

/// Protějšek Demizon.Contracts/Members/UpdateProfileRequest.cs
///
/// Úprava vlastního profilu.
@JsonSerializable()
class UpdateProfileRequest {
  const UpdateProfileRequest({
    required this.name,
    required this.surname,
    this.email,
  });

  final String name;
  final String surname;
  final String? email;

  factory UpdateProfileRequest.fromJson(Map<String, dynamic> json) =>
      _$UpdateProfileRequestFromJson(json);

  Map<String, dynamic> toJson() => _$UpdateProfileRequestToJson(this);
}

/// Protějšek Demizon.Contracts/Members/ChangePasswordRequest.cs
///
/// Změna hesla.
@JsonSerializable()
class ChangePasswordRequest {
  const ChangePasswordRequest({
    required this.currentPassword,
    required this.newPassword,
  });

  final String currentPassword;
  final String newPassword;

  factory ChangePasswordRequest.fromJson(Map<String, dynamic> json) =>
      _$ChangePasswordRequestFromJson(json);

  Map<String, dynamic> toJson() => _$ChangePasswordRequestToJson(this);
}
