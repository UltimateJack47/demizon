import 'package:demizon/api/api_client.dart';
import 'package:demizon/core/providers.dart';
import 'package:demizon/features/attendance/member_attendance_detail_controller.dart';
import 'package:demizon/models/models.dart';
import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

class _RehearsalApi implements ApiClient {
  _RehearsalApi({this.loadFails = false, this.saveFails = false});

  final bool loadFails;
  final bool saveFails;
  int loads = 0;

  static DioException _serverError() => DioException(
    requestOptions: RequestOptions(path: '/api/attendances'),
    response: Response(
      requestOptions: RequestOptions(path: '/api/attendances'),
      statusCode: 500,
    ),
  );

  @override
  Future<Attendance> getMemberRehearsalAttendance(
    int memberId,
    DateTime date,
  ) async {
    loads++;
    if (loadFails) throw _serverError();
    return Attendance(
      id: 7,
      status: 'yes',
      comment: 'přijdu',
      lastUpdated: DateTime(2026, 10, 1),
    );
  }

  @override
  Future<Attendance> upsertMemberRehearsalAttendance(
    int memberId,
    DateTime date,
    UpsertAttendanceRequest request,
  ) async {
    if (saveFails) throw _serverError();
    return Attendance(id: 7, status: request.status, lastUpdated: date);
  }

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

final _target = MemberAttendanceTarget(
  rehearsalDate: DateTime(2026, 10, 16),
  memberId: 3,
  memberName: 'Anna',
);

ProviderContainer _container(ApiClient api) {
  final container = ProviderContainer(
    overrides: [apiClientProvider.overrideWithValue(api)],
  );
  addTearDown(container.dispose);
  container.listen(memberAttendanceDetailProvider(_target), (_, __) {});
  return container;
}

void main() {
  test('načte docházku člena na zkoušku', () async {
    final container = _container(_RehearsalApi());

    final state = await container.read(
      memberAttendanceDetailProvider(_target).future,
    );

    expect(state.status, 'yes');
    expect(state.comment, 'přijdu');
  });

  /// Regrese: chyba načtení se dřív tvářila jako "no" a admin by uložením
  /// přepsal skutečnou odpověď člena.
  test('chyba načtení zkoušky je chyba, ne "no"', () async {
    final container = _container(_RehearsalApi(loadFails: true));

    await expectLater(
      container.read(memberAttendanceDetailProvider(_target).future),
      throwsA(isA<DioException>()),
    );
  });

  test('neúspěšné uložení vrátí false a uvolní formulář', () async {
    final container = _container(_RehearsalApi(saveFails: true));
    await container.read(memberAttendanceDetailProvider(_target).future);

    final saved = await container
        .read(memberAttendanceDetailProvider(_target).notifier)
        .save();

    expect(saved, isFalse);
    expect(
      container
          .read(memberAttendanceDetailProvider(_target))
          .requireValue
          .isSaving,
      isFalse,
    );
  });

  test('znovuotevřený formulář se načte ze serveru, ne z cache', () async {
    final api = _RehearsalApi();
    final container = ProviderContainer(
      overrides: [apiClientProvider.overrideWithValue(api)],
    );
    addTearDown(container.dispose);

    final screen = container.listen(
      memberAttendanceDetailProvider(_target),
      (_, __) {},
    );
    await container.read(memberAttendanceDetailProvider(_target).future);
    screen.close();
    await Future<void>.delayed(Duration.zero);

    container.listen(memberAttendanceDetailProvider(_target), (_, __) {});
    await container.read(memberAttendanceDetailProvider(_target).future);

    expect(api.loads, 2);
  });
}
