import 'package:dio/dio.dart';

import '../../models/models.dart';
import 'token_storage.dart';

/// Připojuje `Bearer` token a stará se o jeho obnovu — protějšek
/// `Demizon.Maui/Services/AuthHandler.cs`.
///
/// Dvě obranné linie, stejně jako v MAUI:
///  1. **proaktivní refresh** v [onRequest], když token vyprší do 5 minut
///     (`TokenStorage.isExpiringSoon`),
///  2. **fallback na 401** v [onError] — request se jednou zopakuje
///     s čerstvým tokenem.
///
/// Souběh je pojištěný jedním sdíleným [Future] ([_refreshInFlight]); v MAUI
/// to byl statický `SemaphoreSlim` s druhou kontrolou po získání zámku
/// (`AuthHandler.cs:7,42-48`).
///
/// Refresh jde vždy přes **samostatnou instanci Dio bez tohoto interceptoru**
/// ([_refreshDio]). MAUI kvůli témuž problému drželo v `TokenRefreshHelper.cs`
/// duplicitní overload s komentářem, že obchází cyklickou DI závislost
/// (`TokenRefreshHelper.cs:36`) — tady žádná duplicita není, jen druhý klient.
class AuthInterceptor extends Interceptor {
  AuthInterceptor({
    required TokenStorage tokenStorage,
    required Dio refreshDio,
    this.onSessionExpired,
  })  : _tokenStorage = tokenStorage,
        _refreshDio = refreshDio;

  /// Cesty, které se nikdy neautorizují ani nerefreshují — jinak by refresh
  /// requestu volal sám sebe.
  static const _anonymousPaths = {'/api/auth/token', '/api/auth/refresh'};

  /// Značka v `RequestOptions.extra`, aby se jeden request nezopakoval dvakrát.
  static const _retriedKey = 'demizon.authRetried';

  final TokenStorage _tokenStorage;
  final Dio _refreshDio;

  /// Volá se, když refresh selhal a session je nenávratně pryč. V MAUI tuhle
  /// roli plnilo `NavigateToLogin()` uvnitř handleru; tady o navigaci
  /// rozhoduje volající (`authControllerProvider`).
  final void Function()? onSessionExpired;

  Future<String?>? _refreshInFlight;

  @override
  Future<void> onRequest(
    RequestOptions options,
    RequestInterceptorHandler handler,
  ) async {
    if (_anonymousPaths.contains(options.path)) {
      handler.next(options);
      return;
    }

    // 1. linie: proaktivní refresh, dokud je token ještě platný.
    if (_tokenStorage.isExpiringSoon) {
      await _refreshToken();
    }

    final token = await _tokenStorage.readAccessToken();
    if (token != null) {
      options.headers['Authorization'] = 'Bearer $token';
    }

    handler.next(options);
  }

  @override
  Future<void> onError(
    DioException err,
    ErrorInterceptorHandler handler,
  ) async {
    final options = err.requestOptions;
    final isUnauthorized = err.response?.statusCode == 401;
    final alreadyRetried = options.extra[_retriedKey] == true;

    if (!isUnauthorized ||
        alreadyRetried ||
        _anonymousPaths.contains(options.path)) {
      handler.next(err);
      return;
    }

    // 2. linie: záchranná síť na 401.
    //
    // Pokud mezitím token obnovil jiný request, stačí zopakovat s tím novým.
    // Když je uložený token pořád ten, se kterým jsme dostali 401, je nutné
    // vynutit skutečný refresh — server ho mohl zneplatnit dřív, než podle
    // `expiresAt` vypršel. (MAUI se tady spoléhalo jen na `IsTokenValid()`
    // a v takovém případě zopakovalo request se stejným tokenem.)
    final usedToken = _bearerOf(options.headers['Authorization']);
    final storedToken = await _tokenStorage.readAccessToken();
    final token = (storedToken != null && storedToken != usedToken)
        ? storedToken
        : await _refreshToken(force: true);

    if (token == null) {
      handler.next(err);
      return;
    }

    options.extra[_retriedKey] = true;
    options.headers['Authorization'] = 'Bearer $token';

    try {
      // Opakuje se přes _refreshDio — má stejnou baseUrl, ale nemá tento
      // interceptor, takže nehrozí rekurze do onRequest/onError.
      final response = await _refreshDio.fetch<dynamic>(options);
      handler.resolve(response);
    } on DioException catch (e) {
      handler.next(e);
    }
  }

  /// Obnoví token. Souběžná volání sdílejí jeden probíhající pokus.
  /// Vrací nový access token, nebo `null`, pokud session skončila.
  Future<String?> _refreshToken({bool force = false}) {
    final inFlight = _refreshInFlight;
    if (inFlight != null) return inFlight;

    final future = _performRefresh(force: force);
    _refreshInFlight = future;
    return future.whenComplete(() => _refreshInFlight = null);
  }

  Future<String?> _performRefresh({required bool force}) async {
    // Druhá kontrola — protějšek re-checku po získání zámku v MAUI
    // (AuthHandler.cs:45-47).
    if (!force && !_tokenStorage.isExpiringSoon) {
      return _tokenStorage.readAccessToken();
    }

    final refreshToken = await _tokenStorage.readRefreshToken();
    if (refreshToken == null) return null;

    final login = await _tokenStorage.login;

    try {
      final response = await _refreshDio.post<Map<String, dynamic>>(
        '/api/auth/refresh',
        data: RefreshRequest(refreshToken: refreshToken).toJson(),
      );

      final data = response.data;
      if (data == null) {
        await _endSession();
        return null;
      }

      final tokens = TokenResponse.fromJson(data);
      await _tokenStorage.saveTokens(tokens, login ?? '');
      return tokens.token;
    } on DioException {
      await _endSession();
      return null;
    } on FormatException {
      await _endSession();
      return null;
    }
  }

  Future<void> _endSession() async {
    await _tokenStorage.clear();
    onSessionExpired?.call();
  }

  static String? _bearerOf(Object? header) {
    final value = header?.toString();
    if (value == null || !value.startsWith('Bearer ')) return null;
    return value.substring('Bearer '.length);
  }
}
