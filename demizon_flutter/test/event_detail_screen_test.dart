import 'package:demizon/api/api_client.dart';
import 'package:demizon/core/auth/token_storage.dart';
import 'package:demizon/core/providers.dart';
import 'package:demizon/features/events/event_detail_screen.dart';
import 'package:demizon/models/models.dart';
import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

class _Tokens implements TokenStorage {
  @override
  Future<String?> get role async => 'Member';

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

class _OfflineApi implements ApiClient {
  @override
  Future<Event> getEvent(int id) async => throw DioException(
        requestOptions: RequestOptions(path: '/api/events/$id'),
        type: DioExceptionType.connectionError,
      );

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

/// Regrese: obrazovka četla `AsyncValue.value`, které při chybě výjimku
/// znovu vyhodí — místo hlášky se ukázala červená (v release šedá) obrazovka.
void main() {
  testWidgets('chyba načtení akce ukáže hlášku, ne pád', (tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          apiClientProvider.overrideWithValue(_OfflineApi()),
          tokenStorageProvider.overrideWithValue(_Tokens()),
        ],
        child: const MaterialApp(home: EventDetailScreen(eventId: 12)),
      ),
    );
    await tester.pumpAndSettle();

    expect(tester.takeException(), isNull);
    expect(find.text('Nepodařilo se načíst akci.'), findsOneWidget);
  });
}
