import 'package:demizon/core/notifications/notification_navigation.dart';
import 'package:demizon/core/notifications/notification_target.dart';
import 'package:flutter_test/flutter_test.dart';

/// Přepis `NotificationNavigationService` — tři cesty (cold / background /
/// foreground) a reset po odhlášení. Bez Firebase, bez routeru.
void main() {
  group('NotificationTarget.parse', () {
    test('eventId má přednost a musí být kladné číslo', () {
      expect(
        NotificationTarget.parse({'eventId': '12', 'rehearsalDate': '2026-05-15'}),
        NotificationTarget.parse({'eventId': '12'}),
      );
      expect(NotificationTarget.parse({'eventId': '0'}), isNull);
      expect(NotificationTarget.parse({'eventId': '-1'}), isNull);
      expect(NotificationTarget.parse({'eventId': 'abc'}), isNull);
    });

    test('rehearsalDate bere kalendářní den', () {
      final target = NotificationTarget.parse({'rehearsalDate': '2026-05-15'});
      expect(target, isNotNull);
      expect(target!.location, '/events/0?rehearsalDate=2026-05-15');
    });

    test('prázdný nebo neznámý payload je null', () {
      expect(NotificationTarget.parse(null), isNull);
      expect(NotificationTarget.parse({}), isNull);
      expect(NotificationTarget.parse({'foo': 'bar'}), isNull);
      expect(NotificationTarget.parse({'rehearsalDate': '  '}), isNull);
    });

    test('payload JSON přežije round-trip', () {
      final event = NotificationTarget.parse({'eventId': '7'})!;
      expect(NotificationTarget.fromPayload(event.toPayload()), event);

      final rehearsal =
          NotificationTarget.parse({'rehearsalDate': '2026-01-16'})!;
      expect(NotificationTarget.fromPayload(rehearsal.toPayload()), rehearsal);

      expect(NotificationTarget.fromPayload('not-json'), isNull);
      expect(NotificationTarget.fromPayload(''), isNull);
    });
  });

  group('NotificationNavigation', () {
    test('cold start uloží cíl a přehraje ho až po markReadyAndFlush', () {
      final nav = NotificationNavigation();
      final opened = <String>[];
      nav.bind(opened.add);

      nav.handle({'eventId': '42'});
      expect(opened, isEmpty);

      nav.markReadyAndFlush();
      expect(opened, ['/events/42']);

      // Druhý flush nic nepřehraje.
      nav.markReadyAndFlush();
      expect(opened, ['/events/42']);
    });

    test('když je shell ready, naviguje hned', () {
      final nav = NotificationNavigation();
      final opened = <String>[];
      nav.bind(opened.add);
      nav.markReadyAndFlush();

      nav.handle({'rehearsalDate': '2026-05-15'});
      expect(opened, ['/events/0?rehearsalDate=2026-05-15']);
    });

    test('reset zahodí pending i ready, ať se po odhlášení neotevře starý cíl',
        () {
      final nav = NotificationNavigation();
      final opened = <String>[];
      nav.bind(opened.add);
      nav.markReadyAndFlush();

      nav.handle({'eventId': '8'});
      expect(opened, ['/events/8']);

      nav.reset();
      nav.handle({'eventId': '9'});
      expect(opened, ['/events/8'], reason: 'po logoutu čeká na další login');

      nav.markReadyAndFlush();
      expect(opened, ['/events/8', '/events/9']);
    });

    test('handlePayload čte JSON z lokální notifikace', () {
      final nav = NotificationNavigation();
      final opened = <String>[];
      nav.bind(opened.add);
      nav.markReadyAndFlush();

      final payload =
          NotificationTarget.parse({'eventId': '3'})!.toPayload();
      nav.handlePayload(payload);
      expect(opened, ['/events/3']);
    });
  });
}
