import 'package:json_annotation/json_annotation.dart';

part 'register_device_request.g.dart';

/// Protějšek Demizon.Contracts/Notifications/RegisterDeviceRequest.cs
///
/// Registrace zařízení pro push notifikace. `token` je FCM token,
/// `platform` je identifikace platformy (např. "android").
@JsonSerializable()
class RegisterDeviceRequest {
  const RegisterDeviceRequest({
    required this.token,
    required this.platform,
  });

  final String token;
  final String platform;

  factory RegisterDeviceRequest.fromJson(Map<String, dynamic> json) =>
      _$RegisterDeviceRequestFromJson(json);

  Map<String, dynamic> toJson() => _$RegisterDeviceRequestToJson(this);
}
