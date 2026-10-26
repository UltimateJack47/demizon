import 'package:dio/dio.dart';

import '../../models/models.dart';

/// Role správce krojárky tak, jak ji posílá server v `roles`.
const kCostumeKeeperRole = 'CostumeKeeper';

enum CostumeScanKind { item, member }

/// Co bylo naskenováno nebo napsáno — protějšek `CostumeScan.Parse` na serveru.
///
/// Bere jen poslední segment URL, takže štítky přežijí změnu domény. Text bez
/// URL je ručně napsaný kód kusu.
class CostumeScan {
  const CostumeScan(this.kind, this.value);

  final CostumeScanKind kind;
  final String value;

  static CostumeScan? parse(String? text) {
    final trimmed = text?.trim();
    if (trimmed == null || trimmed.isEmpty) return null;

    final uri = Uri.tryParse(trimmed);
    final path = uri != null && (uri.scheme == 'http' || uri.scheme == 'https') ? uri.path : trimmed;
    final segments = path.split('/').where((s) => s.isNotEmpty).toList();
    if (segments.isEmpty) return null;

    final last = Uri.decodeComponent(segments.last);
    final marker = segments.length >= 2 ? segments[segments.length - 2].toLowerCase() : null;
    return CostumeScan(marker == 'm' ? CostumeScanKind.member : CostumeScanKind.item, last);
  }

  @override
  bool operator ==(Object other) => other is CostumeScan && other.kind == kind && other.value == value;

  @override
  int get hashCode => Object.hash(kind, value);
}

/// Text chyby pro uživatele: server posílá `{ error }` česky, jinak obecná hláška.
String apiErrorText(Object error, String fallback) {
  if (error is DioException) {
    final data = error.response?.data;
    if (data is Map && data['error'] is String) return data['error'] as String;
    if (error.type == DioExceptionType.connectionError ||
        error.type == DioExceptionType.connectionTimeout ||
        error.type == DioExceptionType.receiveTimeout) {
      return 'Nepodařilo se spojit se serverem.';
    }
  }
  return fallback;
}

String transferStatusLabel(String status) => switch (status) {
      'Draft' => 'rozpracovaná',
      'Pending' => 'čeká na potvrzení',
      'Confirmed' => 'potvrzená',
      'Rejected' => 'odmítnutá',
      'Cancelled' => 'zrušená',
      _ => status,
    };

String itemStateLabel(String state) => switch (state) {
      'Available' => 'dostupný',
      'InMaintenance' => 'v údržbě',
      'Lost' => 'ztracený',
      'Retired' => 'vyřazený',
      _ => state,
    };

/// Druh předávky z pohledu toho, kdo ji zakládá.
enum HandoverMode {
  /// Správce: z krojárky členovi.
  issue,

  /// Správce: od člena zpátky do krojárky.
  returnToStore,

  /// Člen: ze svých kusů kolegovi.
  peer;

  static HandoverMode fromQuery(String? value) => switch (value) {
        'issue' => HandoverMode.issue,
        'return' => HandoverMode.returnToStore,
        _ => HandoverMode.peer,
      };

  String get query => switch (this) {
        HandoverMode.issue => 'issue',
        HandoverMode.returnToStore => 'return',
        HandoverMode.peer => 'peer',
      };

  String get title => switch (this) {
        HandoverMode.issue => 'Výdej z krojárky',
        HandoverMode.returnToStore => 'Příjem do krojárky',
        HandoverMode.peer => 'Předat kolegovi',
      };
}

/// Výsledek přidání naskenovaného kusu do sbíraného seznamu.
/// `counterpart` vyplní u příjmu vracejícího podle držitele kusu.
class CollectOutcome {
  const CollectOutcome.error(this.error) : counterpart = null;
  const CollectOutcome.added({this.counterpart}) : error = null;

  final String? error;
  final CostumeParty? counterpart;

  bool get isAdded => error == null;
}

/// Okamžitá kontrola kusu při sběru — stejná pravidla jako web (`Handover.razor`).
/// Definitivně všechno ověří server při založení předávky.
CollectOutcome checkCollectedItem({
  required HandoverMode mode,
  required CostumeItemCard card,
  required int myMemberId,
  required int? counterpartId,
}) {
  final item = card.item;
  if (item.state != 'Available') {
    return CollectOutcome.error('Kus ${item.code} je ${itemStateLabel(item.state)}.');
  }

  final holders = card.holders;
  switch (mode) {
    case HandoverMode.issue:
      if (card.inStore < 1) return CollectOutcome.error('Kus ${item.code} není v krojárce.');
      return const CollectOutcome.added();
    case HandoverMode.peer:
      if (holders.every((h) => h.member.memberId != myMemberId)) {
        return CollectOutcome.error('Kus ${item.code} nemáš u sebe.');
      }
      return const CollectOutcome.added();
    case HandoverMode.returnToStore:
      if (holders.isEmpty) return CollectOutcome.error('Kus ${item.code} už v krojárce je.');
      if (counterpartId == null) {
        if (holders.length > 1) {
          return CollectOutcome.error('${item.code} má víc lidí — naskenuj QR toho, kdo vrací.');
        }
        return CollectOutcome.added(counterpart: holders.single.member);
      }
      if (holders.every((h) => h.member.memberId != counterpartId)) {
        return CollectOutcome.error('Kus ${item.code} tenhle člen u sebe nemá.');
      }
      return const CollectOutcome.added();
  }
}
