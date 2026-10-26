import 'package:demizon/api/api_client.dart';
import 'package:demizon/core/auth/token_storage.dart';
import 'package:demizon/core/providers.dart';
import 'package:demizon/features/dances/dance_detail_controller.dart';
import 'package:demizon/features/events/event_detail_controller.dart';
import 'package:demizon/models/models.dart';
import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

class _Tokens implements TokenStorage {
  @override
  Future<List<String>> get roles async => const [];

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

DioException _status(int code) => DioException(
      requestOptions: RequestOptions(path: '/api'),
      response: Response(
        requestOptions: RequestOptions(path: '/api'),
        statusCode: code,
      ),
    );

class _Api implements ApiClient {
  /// Stavový kód, kterým odpoví `GET /api/attendances/rehearsal`; null = 200.
  int? rehearsalStatus;
  bool photosFail = false;
  int rehearsalLoads = 0;
  int danceLoads = 0;

  @override
  Future<List<Event>> getEventsByMonth(int year, int month) async => const [];

  @override
  Future<Attendance> getRehearsalAttendance(DateTime date) async {
    rehearsalLoads++;
    final status = rehearsalStatus;
    if (status != null) throw _status(status);
    return Attendance(id: 1, status: 'yes', lastUpdated: date);
  }

  @override
  Future<Dance> getDance(int id) async {
    danceLoads++;
    return Dance(id: id, name: 'Verbuňk', videos: const []);
  }

  @override
  Future<List<GalleryPhoto>> getDancePhotos(int id) async {
    if (photosFail) throw _status(500);
    return const [];
  }

  @override
  Future<List<DanceDocument>> getDanceDocuments(int id) async => const [];

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

final _rehearsal = EventDetailArgs(rehearsalDate: DateTime(2026, 10, 16));

void main() {
  late _Api api;
  late ProviderContainer container;

  setUp(() {
    api = _Api();
    container = ProviderContainer(
      overrides: [
        apiClientProvider.overrideWithValue(api),
        tokenStorageProvider.overrideWithValue(_Tokens()),
      ],
    );
  });

  tearDown(() => container.dispose());

  /// Otevře obrazovku, počká na data a zase ji zavře.
  Future<T> open<T>(ProviderListenable<AsyncValue<T>> provider,
      Future<T> Function() future) async {
    final screen = container.listen(provider, (_, __) {});
    try {
      return await future();
    } finally {
      screen.close();
      await Future<void>.delayed(Duration.zero);
    }
  }

  group('detail akce — zkouška', () {
    test('bez docházky (404) je "nepřijdu"', () async {
      api.rehearsalStatus = 404;

      final state = await open(eventDetailProvider(_rehearsal),
          () => container.read(eventDetailProvider(_rehearsal).future));

      expect(state.status, 'no');
    });

    /// Regrese: každá chyba se tvářila jako "nepřijdu" a člen by uložením
    /// přepsal svou skutečnou odpověď.
    test('jiná chyba je chyba, ne "nepřijdu"', () async {
      api.rehearsalStatus = 500;

      await expectLater(
        open(eventDetailProvider(_rehearsal),
            () => container.read(eventDetailProvider(_rehearsal).future)),
        throwsA(isA<DioException>()),
      );
    });

    test('znovu otevřený detail se načte ze serveru, ne z cache', () async {
      api.rehearsalStatus = 500;
      await expectLater(
        open(eventDetailProvider(_rehearsal),
            () => container.read(eventDetailProvider(_rehearsal).future)),
        throwsA(isA<DioException>()),
      );

      api.rehearsalStatus = null;
      final state = await open(eventDetailProvider(_rehearsal),
          () => container.read(eventDetailProvider(_rehearsal).future));

      expect(state.status, 'yes');
      expect(api.rehearsalLoads, 2);
    });
  });

  group('detail tance', () {
    test('neúspěšné fotky nezastaví detail', () async {
      api.photosFail = true;

      final detail = await open(danceDetailProvider(5),
          () => container.read(danceDetailProvider(5).future));

      expect(detail.dance.name, 'Verbuňk');
      expect(detail.hasPhotos, isFalse);
    });

    test('znovu otevřený detail se načte ze serveru, ne z cache', () async {
      await open(danceDetailProvider(5),
          () => container.read(danceDetailProvider(5).future));
      await open(danceDetailProvider(5),
          () => container.read(danceDetailProvider(5).future));

      expect(api.danceLoads, 2);
    });
  });
}
