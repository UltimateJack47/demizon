import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../models/models.dart';
import '../providers.dart';
import 'token_storage.dart';

/// Stav přihlášení a operace nad ním — protějšek `LoginViewModel.LoginAsync`,
/// `ProfileViewModel.LogoutAsync` a auto-loginu z `App.xaml.cs`
/// (`TryAutoLoginAsync`).
///
/// Načítání session (`AsyncLoading`) řeší Riverpod, takže tu není žádný
/// `IsBusy` jako v MAUI.

/// Session uživatele.
sealed class AuthState {
  const AuthState();
}

/// Nikdo není přihlášen — router má poslat na `/login`.
class Unauthenticated extends AuthState {
  const Unauthenticated();
}

/// Platná session. [role] a [memberId] jsou předtažené z úložiště, aby se na ně
/// obrazovky nemusely ptát asynchronně (v MAUI to dělal každý ViewModel zvlášť,
/// např. `EventDetailViewModel.LoadAsync`).
class Authenticated extends AuthState {
  const Authenticated({
    required this.login,
    required this.role,
    this.memberId,
    this.gcalConnected = false,
  });

  final String login;
  final String? role;
  final int? memberId;
  final bool gcalConnected;

  /// V MAUI porovnáváno case-insensitive proti "Admin".
  bool get isAdmin => role?.toLowerCase() == 'admin';
}

/// Chyba přihlášení s hláškou pro uživatele. Texty jsou převzaté doslova
/// z `LoginViewModel.cs:39-50`.
class AuthException implements Exception {
  const AuthException(this.message);

  final String message;

  @override
  String toString() => message;
}

final authControllerProvider =
    AsyncNotifierProvider<AuthController, AuthState>(AuthController.new);

/// Zkratky pro router (`core/router.dart`), aby nemusel rozbalovat
/// `AsyncValue` ručně.
extension AuthSessionX on AsyncValue<AuthState> {
  /// `true` jen při potvrzené session. Během obnovy (`AsyncLoading`) je `false`.
  bool get isAuthenticated => valueOrNull is Authenticated;

  /// `true` jen během úvodního auto-loginu při startu (`build()`), kdy stav
  /// ještě nemá žádnou hodnotu. Router tuto fázi překrývá splashem, aby
  /// přihlášenému neprobleskla přihlašovací obrazovka.
  ///
  /// Přihlašování samo stav na `AsyncLoading` nepřepíná — jinak by router
  /// uprostřed requestu zahodil přihlašovací obrazovku i s chybovou hláškou.
  bool get isRestoring => isLoading && !hasValue;

  Authenticated? get session => valueOrNull is Authenticated
      ? valueOrNull! as Authenticated
      : null;
}

/// Most mezi Riverpodem a `GoRouter.refreshListenable` — router se
/// překreslí, kdykoli se změní stav session (přihlášení, odhlášení,
/// vypršení tokenu).
final authRefreshListenableProvider = Provider<Listenable>((ref) {
  final notifier = _AuthRefreshNotifier();
  ref.listen<AsyncValue<AuthState>>(
    authControllerProvider,
    (_, __) => notifier.notify(),
  );
  ref.onDispose(notifier.dispose);
  return notifier;
});

class _AuthRefreshNotifier extends ChangeNotifier {
  void notify() => notifyListeners();
}

class AuthController extends AsyncNotifier<AuthState> {
  TokenStorage get _tokens => ref.read(tokenStorageProvider);

  @override
  Future<AuthState> build() async {
    // Refresh v interceptoru selhal → session končí i tady.
    final subscription = ref.watch(sessionExpiredProvider).stream.listen((_) {
      state = const AsyncData(Unauthenticated());
    });
    ref.onDispose(subscription.cancel);

    return _restoreSession();
  }

  /// Auto-login při startu — protějšek `App.TryAutoLoginAsync()`.
  Future<AuthState> _restoreSession() async {
    try {
      await _tokens.initialize();

      if (!_tokens.isExpiringSoon) {
        return await _authenticatedFromStorage();
      }

      final refreshToken = await _tokens.readRefreshToken();
      if (refreshToken == null) return const Unauthenticated();

      final login = await _tokens.login;
      final response = await ref
          .read(apiClientProvider)
          .refresh(RefreshRequest(refreshToken: refreshToken));

      await _tokens.saveTokens(response, login ?? '');
      return await _authenticatedFromStorage();
    } catch (_) {
      // MAUI zde rovněž jen vyčistilo úložiště a zůstalo na přihlášení.
      await _tokens.clear();
      return const Unauthenticated();
    }
  }

  /// Přihlášení jménem a heslem. Uloží tokeny a přepne stav na [Authenticated].
  /// Při neúspěchu vyhodí [AuthException] s českou hláškou a stav vrátí zpět
  /// na [Unauthenticated].
  Future<void> login(String login, String password) async {
    // Stav zamerne NEprepiname na AsyncLoading: router cte isLoading jako
    // "obnovuje se session" a odskocil by na splash, cimz by se prihlasovaci
    // obrazovka uprostred requestu zahodila. Spinner si drzi obrazovka sama.
    try {
      final response = await ref
          .read(apiClientProvider)
          .login(TokenRequest(login: login, password: password));

      await _tokens.saveTokens(response, login);
      state = AsyncData(await _authenticatedFromStorage());
    } on DioException catch (e) {
      state = const AsyncData(Unauthenticated());
      throw AuthException(_loginErrorMessage(e));
    } catch (_) {
      state = const AsyncData(Unauthenticated());
      throw const AuthException('Přihlášení selhalo. Zkuste to prosím znovu.');
    }
  }

  /// Odhlášení — protějšek `ProfileViewModel.LogoutAsync()`.
  /// Navigaci na přihlášení řeší router podle stavu, ne tato metoda.
  Future<void> logout() async {
    await _tokens.clear();
    state = const AsyncData(Unauthenticated());
  }

  Future<Authenticated> _authenticatedFromStorage() async {
    return Authenticated(
      login: await _tokens.login ?? '',
      role: await _tokens.role,
      memberId: await _tokens.memberId,
      gcalConnected: await _tokens.gcalConnected,
    );
  }

  static String _loginErrorMessage(DioException e) {
    if (e.response?.statusCode == 401) {
      return 'Nesprávné přihlašovací jméno nebo heslo.';
    }
    return switch (e.type) {
      DioExceptionType.connectionError ||
      DioExceptionType.connectionTimeout ||
      DioExceptionType.sendTimeout ||
      DioExceptionType.receiveTimeout =>
        'Nelze se připojit k serveru. Zkontrolujte síťové připojení.',
      _ => 'Přihlášení selhalo. Zkuste to prosím znovu.',
    };
  }
}
