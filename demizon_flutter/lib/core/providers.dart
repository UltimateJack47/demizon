import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/api_client.dart';
import 'api_config.dart';
import 'auth/auth_interceptor.dart';
import 'auth/token_storage.dart';

/// Kořenové providery infrastruktury — protějšek registrací v
/// `Demizon.Maui/MauiProgram.cs` (`AddRefitClient` + `AddTransient<AuthHandler>`
/// + `AddSingleton<TokenStorage>`).

/// Bezpečné úložiště tokenů. Singleton na celou aplikaci — drží in-memory
/// cache expirace, takže se nesmí vytvářet opakovaně.
///
/// Pozor: `TokenStorage.initialize()` je nutné zavolat jednou při startu
/// (v `main.dart`), jinak cache neví o tokenu z minulého běhu a první request
/// zbytečně refreshuje.
final tokenStorageProvider = Provider<TokenStorage>((ref) => TokenStorage());

/// Signál „session skončila“. Interceptor ho vystřelí, když refresh selže;
/// `authControllerProvider` na něj reaguje odhlášením a router přesměrováním
/// na přihlášení. V MAUI to řešil `AuthHandler.NavigateToLogin()` přímo uvnitř
/// HTTP vrstvy.
class SessionExpiredSignal {
  final _controller = StreamController<void>.broadcast();

  Stream<void> get stream => _controller.stream;

  void fire() {
    if (!_controller.isClosed) _controller.add(null);
  }

  void dispose() => _controller.close();
}

final sessionExpiredProvider = Provider<SessionExpiredSignal>((ref) {
  final signal = SessionExpiredSignal();
  ref.onDispose(signal.dispose);
  return signal;
});

/// Dio bez [AuthInterceptor]. Slouží k obnově tokenu a k zopakování requestu
/// po 401 — díky tomu nemůže vzniknout rekurze ani cyklická závislost, kterou
/// v MAUI obcházel duplicitní overload v `TokenRefreshHelper.cs:36`.
final refreshDioProvider = Provider<Dio>((ref) {
  final dio = Dio(_baseOptions());
  dio.interceptors.add(_dateTimeQueryInterceptor);
  ref.onDispose(dio.close);
  return dio;
});

/// Hlavní HTTP klient — s autorizací a obnovou tokenu.
final dioProvider = Provider<Dio>((ref) {
  final dio = Dio(_baseOptions());
  dio.interceptors.add(_dateTimeQueryInterceptor);
  dio.interceptors.add(
    AuthInterceptor(
      tokenStorage: ref.watch(tokenStorageProvider),
      refreshDio: ref.watch(refreshDioProvider),
      onSessionExpired: () => ref.read(sessionExpiredProvider).fire(),
    ),
  );
  ref.onDispose(dio.close);
  return dio;
});

/// Typovaný klient API (Retrofit).
final apiClientProvider = Provider<ApiClient>(
  (ref) => ApiClient(ref.watch(dioProvider)),
);

BaseOptions _baseOptions() => BaseOptions(
      baseUrl: ApiConfig.baseUrl,
      connectTimeout: const Duration(seconds: 20),
      receiveTimeout: const Duration(seconds: 30),
      // TODO(verify): MAUI používal výchozí timeout HttpClient (100 s).
      // Ověřit, že 20/30 s stačí i na pomalém mobilním připojení.
      headers: const {'Accept': 'application/json'},
    );

/// Dio by na `DateTime` v query parametrech zavolalo `toString()`
/// (`2026-08-31 00:00:00.000`). Refit posílal ISO 8601 round-trip, takže se
/// hodnoty převádějí na stejný tvar.
///
/// TODO(verify): až bude vygenerovaný `api_client.g.dart`, zkontrolovat, jestli
/// retrofit_generator náhodou `DateTime` nepřevádí sám — pak je tenhle
/// interceptor bez efektu (hodnota už je `String`), ne však škodlivý.
final _dateTimeQueryInterceptor = InterceptorsWrapper(
  onRequest: (options, handler) {
    options.queryParameters = options.queryParameters.map(
      (key, value) => MapEntry(
        key,
        value is DateTime ? value.toIso8601String() : value,
      ),
    );
    handler.next(options);
  },
);
