import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../../models/models.dart';

/// Bezpečné úložiště přihlašovacích údajů — protějšek
/// `Demizon.Maui/Services/TokenStorage.cs`.
///
/// Klíče jsou shodné s MAUI verzí (7 položek), takže formát v úložišti
/// odpovídá 1:1. `SecureStorage.Default` nahrazuje `flutter_secure_storage`.
///
/// Stejně jako v MAUI (`TokenStorage.cs:16-17,43-48`) drží třída **in-memory
/// cache** expirace a příznaku „mám token“. Důvod byl původně neblokování UI
/// threadu; ve Flutteru je motivace stejná — [isExpiringSoon] musí být
/// synchronní, aby ho mohl interceptor volat na každém requestu bez čekání
/// na I/O do keychainu.
class TokenStorage {
  TokenStorage({FlutterSecureStorage? storage})
      : _storage = storage ?? const FlutterSecureStorage();

  // Názvy klíčů převzaté beze změny z TokenStorage.cs:7-13.
  static const _accessTokenKey = 'access_token';
  static const _refreshTokenKey = 'refresh_token';
  static const _expiresAtKey = 'expires_at';
  static const _roleKey = 'role';
  static const _loginKey = 'login';
  static const _memberIdKey = 'member_id';
  static const _gcalConnectedKey = 'gcal_connected';

  /// Práh proaktivního refreshe — shodný s `IsTokenValid()` v MAUI.
  static const refreshThreshold = Duration(minutes: 5);

  final FlutterSecureStorage _storage;

  // In-memory cache, aby šlo o platnosti tokenu rozhodnout synchronně.
  DateTime? _cachedExpiresAt;
  bool _hasAccessToken = false;

  /// Obnoví in-memory cache z úložiště. Volat jednou při startu aplikace
  /// (protějšek `InitializeAsync`, v MAUI se volalo z `App.xaml.cs`).
  Future<void> initialize() async {
    final token = await _storage.read(key: _accessTokenKey);
    final expiresAtRaw = await _storage.read(key: _expiresAtKey);

    _hasAccessToken = token != null;
    _cachedExpiresAt =
        expiresAtRaw != null ? DateTime.tryParse(expiresAtRaw)?.toUtc() : null;
  }

  /// Uloží odpověď tokenového endpointu a přihlašovací jméno.
  /// Protějšek `SaveAsync(TokenResponse, string login)`.
  Future<void> saveTokens(TokenResponse response, String login) async {
    final expiresAt =
        DateTime.now().toUtc().add(Duration(seconds: response.expiresIn));

    await _storage.write(key: _accessTokenKey, value: response.token);
    await _storage.write(key: _refreshTokenKey, value: response.refreshToken);
    await _storage.write(
      key: _expiresAtKey,
      // Odpovídá C# formátu "O" (round-trip ISO 8601).
      value: expiresAt.toIso8601String(),
    );
    await _storage.write(key: _roleKey, value: response.role);
    await _storage.write(key: _loginKey, value: login);
    if (response.memberId != 0) {
      await _storage.write(
        key: _memberIdKey,
        value: response.memberId.toString(),
      );
    }
    await _storage.write(
      key: _gcalConnectedKey,
      value: response.isGoogleCalendarConnected ? 'true' : 'false',
    );

    _cachedExpiresAt = expiresAt;
    _hasAccessToken = true;
  }

  Future<String?> readAccessToken() => _storage.read(key: _accessTokenKey);

  Future<String?> readRefreshToken() => _storage.read(key: _refreshTokenKey);

  /// `true`, pokud token chybí nebo vyprší do [refreshThreshold].
  /// Negace `IsTokenValid()` z MAUI — synchronně, nad in-memory cache.
  bool get isExpiringSoon {
    final expiresAt = _cachedExpiresAt;
    if (!_hasAccessToken || expiresAt == null) return true;
    return !expiresAt.isAfter(DateTime.now().toUtc().add(refreshThreshold));
  }

  Future<String?> get role => _storage.read(key: _roleKey);

  Future<String?> get login => _storage.read(key: _loginKey);

  Future<int?> get memberId async {
    final raw = await _storage.read(key: _memberIdKey);
    return raw == null ? null : int.tryParse(raw);
  }

  Future<bool> get gcalConnected async {
    final raw = await _storage.read(key: _gcalConnectedKey);
    return raw?.toLowerCase() == 'true';
  }

  /// Smaže všech 7 klíčů a vyprázdní cache. Protějšek `Clear()`.
  Future<void> clear() async {
    await Future.wait([
      _storage.delete(key: _accessTokenKey),
      _storage.delete(key: _refreshTokenKey),
      _storage.delete(key: _expiresAtKey),
      _storage.delete(key: _roleKey),
      _storage.delete(key: _loginKey),
      _storage.delete(key: _memberIdKey),
      _storage.delete(key: _gcalConnectedKey),
    ]);

    _cachedExpiresAt = null;
    _hasAccessToken = false;
  }
}
