import 'package:demizon/core/notifications/notification_target.dart';
import 'package:demizon/features/costumes/costume_logic.dart';
import 'package:demizon/models/models.dart';
import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';

CostumeItem _item({String code = 'K0001', String state = 'Available', String mode = 'Individual'}) => CostumeItem(
      id: 1,
      code: code,
      partId: 1,
      partName: 'Vesta',
      costumeName: 'Kopanický',
      gender: 'Male',
      trackingMode: mode,
      size: '4',
      quantity: 1,
      state: state,
    );

CostumeHolder _holder(int memberId) =>
    CostumeHolder(member: CostumeParty(memberId: memberId, fullName: 'Člen $memberId', isDeleted: false), quantity: 1);

CostumeItemCard _card({int inStore = 0, List<int> holders = const [], String state = 'Available'}) =>
    CostumeItemCard(item: _item(state: state), inStore: inStore, holders: [for (final h in holders) _holder(h)]);

void main() {
  group('sken (protějšek CostumeScan.Parse)', () {
    test('kód kusu z URL štítku i z jiné domény', () {
      expect(CostumeScan.parse('https://demizon.cz/k/K0042'), const CostumeScan(CostumeScanKind.item, 'K0042'));
      expect(CostumeScan.parse('https://jinde.cz/k/K0042'), const CostumeScan(CostumeScanKind.item, 'K0042'));
    });

    test('ručně napsaný kód', () {
      expect(CostumeScan.parse('  k0042 '), const CostumeScan(CostumeScanKind.item, 'k0042'));
    });

    test('QR člena podle segmentu m', () {
      expect(CostumeScan.parse('https://demizon.cz/m/AbC-12_xY'), const CostumeScan(CostumeScanKind.member, 'AbC-12_xY'));
      expect(CostumeScan.parse('https://demizon.cz/M/AbC'), const CostumeScan(CostumeScanKind.member, 'AbC'));
    });

    test('prázdný vstup není nic', () {
      expect(CostumeScan.parse(null), isNull);
      expect(CostumeScan.parse('   '), isNull);
      expect(CostumeScan.parse('https://demizon.cz/'), isNull);
    });
  });

  group('chyby z API', () {
    test('text ze serveru má přednost', () {
      final error = DioException(
        requestOptions: RequestOptions(path: '/api'),
        response: Response(requestOptions: RequestOptions(path: '/api'), statusCode: 400, data: {'error': 'Kus je v údržbě.'}),
      );
      expect(apiErrorText(error, 'obecná'), 'Kus je v údržbě.');
    });

    test('bez textu obecná hláška', () {
      expect(apiErrorText(Exception('x'), 'obecná'), 'obecná');
    });
  });

  group('sběr kusů do předávky', () {
    test('výdej jen toho, co je v krojárce', () {
      expect(checkCollectedItem(mode: HandoverMode.issue, card: _card(inStore: 1), myMemberId: 9, counterpartId: null).isAdded,
          isTrue);
      expect(checkCollectedItem(mode: HandoverMode.issue, card: _card(holders: [2]), myMemberId: 9, counterpartId: null).error,
          contains('není v krojárce'));
    });

    test('kolegovi jen to, co mám já', () {
      expect(checkCollectedItem(mode: HandoverMode.peer, card: _card(holders: [9]), myMemberId: 9, counterpartId: 3).isAdded,
          isTrue);
      expect(checkCollectedItem(mode: HandoverMode.peer, card: _card(holders: [2]), myMemberId: 9, counterpartId: 3).error,
          contains('nemáš u sebe'));
    });

    test('příjem doplní vracejícího podle jediného držitele', () {
      final outcome =
          checkCollectedItem(mode: HandoverMode.returnToStore, card: _card(holders: [2]), myMemberId: 9, counterpartId: null);
      expect(outcome.isAdded, isTrue);
      expect(outcome.counterpart?.memberId, 2);
    });

    test('příjem šarže od více lidí chce QR vracejícího', () {
      expect(
          checkCollectedItem(mode: HandoverMode.returnToStore, card: _card(holders: [2, 3]), myMemberId: 9, counterpartId: null)
              .error,
          contains('QR'));
    });

    test('příjem kusu, který vracející nemá', () {
      expect(checkCollectedItem(mode: HandoverMode.returnToStore, card: _card(holders: [3]), myMemberId: 9, counterpartId: 2)
          .error, contains('nemá'));
    });

    test('kus v údržbě nejde nikam', () {
      expect(
          checkCollectedItem(mode: HandoverMode.issue, card: _card(inStore: 1, state: 'InMaintenance'), myMemberId: 9, counterpartId: null)
              .error,
          contains('v údržbě'));
    });
  });

  group('notifikace krojárky', () {
    test('předávka otevře detail', () {
      final target = NotificationTarget.parse({'costumeTransferId': '42'});
      expect(target?.location, '/costume-transfers/42');
      expect(NotificationTarget.fromPayload(target!.toPayload()), target);
    });

    test('připomínka otevře Moje kroje', () {
      final target = NotificationTarget.parse({'costumes': 'mine'});
      expect(target?.location, '/profile/costumes');
      expect(NotificationTarget.fromPayload(target!.toPayload()), target);
    });

    test('akce má přednost před krojárkou', () {
      expect(NotificationTarget.parse({'eventId': '5', 'costumeTransferId': '42'})?.location, '/events/5');
    });
  });

  group('kontrakt', () {
    test('předávka z JSON i s krojárkou jako stranou a časy v UTC', () {
      final transfer = CostumeTransfer.fromJson({
        'id': 7,
        'status': 'Pending',
        'from': null,
        'to': {'memberId': 2, 'fullName': 'Jana Nová', 'isDeleted': false},
        'createdBy': {'memberId': 1, 'fullName': 'Krojařka', 'isDeleted': false},
        'createdAt': '2026-10-26T10:00:00Z',
        'submittedAt': null,
        'resolvedAt': null,
        'resolvedBy': null,
        'note': null,
        'rejectionReason': null,
        'lines': [
          {
            'id': 3,
            'item': _item().toJson(),
            'quantity': 1,
            'conditionNote': null,
            'photos': [
              {'id': 9, 'uploadedAt': '2026-10-26T10:05:00Z', 'capturedAt': null, 'isStale': false},
            ],
          },
        ],
      });

      expect(transfer.fromName, 'Krojárka');
      expect(transfer.toName, 'Jana Nová');
      expect(transfer.isPending, isTrue);
      expect(transfer.createdAt.isUtc, isTrue);
      expect(transfer.lines.single.photos.single.id, 9);
      expect(transfer.lines.single.item.description, 'Kopanický · Vesta · vel. 4');
    });
  });
}
