import 'package:json_annotation/json_annotation.dart';

part 'auth.g.dart';

/// Protějšek Demizon.Contracts/Auth/TokenRequest.cs
///
/// Přihlašovací požadavek.
@JsonSerializable()
class TokenRequest {
  const TokenRequest({
    required this.login,
    required this.password,
  });

  final String login;
  final String password;

  factory TokenRequest.fromJson(Map<String, dynamic> json) =>
      _$TokenRequestFromJson(json);

  Map<String, dynamic> toJson() => _$TokenRequestToJson(this);
}

/// Protějšek Demizon.Contracts/Auth/TokenResponse.cs
///
/// Odpověď na přihlášení i na obnovu tokenu.
/// `expiresIn` je v sekundách.
@JsonSerializable()
class TokenResponse {
  const TokenResponse({
    required this.token,
    required this.refreshToken,
    required this.expiresIn,
    required this.role,
    this.isGoogleCalendarConnected = false,
    this.memberId = 0,
  });

  final String token;
  final String refreshToken;
  final int expiresIn;
  final String role;
  final bool isGoogleCalendarConnected;
  final int memberId;

  factory TokenResponse.fromJson(Map<String, dynamic> json) =>
      _$TokenResponseFromJson(json);

  Map<String, dynamic> toJson() => _$TokenResponseToJson(this);
}

/// Protějšek Demizon.Contracts/Auth/RefreshRequest.cs
///
/// Požadavek na obnovu access tokenu.
@JsonSerializable()
class RefreshRequest {
  const RefreshRequest({
    required this.refreshToken,
  });

  final String refreshToken;

  factory RefreshRequest.fromJson(Map<String, dynamic> json) =>
      _$RefreshRequestFromJson(json);

  Map<String, dynamic> toJson() => _$RefreshRequestToJson(this);
}
