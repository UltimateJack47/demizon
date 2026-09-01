import 'package:demizon/core/auth/auth_controller.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

/// Regresní testy na sémantiku stavu session, kterou čte router.
///
/// Pozadí: první verze měla `isRestoring => isLoading` a `login()` přepínalo
/// stav na `AsyncLoading`. Router to vyhodnotil jako „obnovuje se session",
/// odskočil na splash a zahodil přihlašovací obrazovku uprostřed requestu —
/// chybová hláška se nikdy nezobrazila a `setState()` spadl na disposed widgetu.
void main() {
  group('isRestoring', () {
    test('platí při úvodní obnově, kdy stav ještě nemá hodnotu', () {
      const state = AsyncLoading<AuthState>();
      expect(state.isRestoring, isTrue);
    });

    test('neplatí, když stav hodnotu má — tj. během přihlašování', () {
      const state = AsyncData<AuthState>(Unauthenticated());
      expect(state.isRestoring, isFalse);
    });

    test('neplatí pro přihlášeného uživatele', () {
      const state = AsyncData<AuthState>(
        Authenticated(login: 'jan', role: 'Admin'),
      );
      expect(state.isRestoring, isFalse);
    });
  });

  group('isAuthenticated', () {
    test('je true jen pro potvrzenou session', () {
      const authenticated = AsyncData<AuthState>(
        Authenticated(login: 'jan', role: 'Member'),
      );
      expect(authenticated.isAuthenticated, isTrue);
      expect(authenticated.session?.login, 'jan');
    });

    test('je false během obnovy i pro odhlášeného', () {
      expect(const AsyncLoading<AuthState>().isAuthenticated, isFalse);
      expect(
        const AsyncData<AuthState>(Unauthenticated()).isAuthenticated,
        isFalse,
      );
    });

    test('session je null, když nikdo není přihlášen', () {
      expect(const AsyncData<AuthState>(Unauthenticated()).session, isNull);
      expect(const AsyncLoading<AuthState>().session, isNull);
    });
  });
}
