import 'dart:typed_data';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/providers.dart';
import '../../models/models.dart';

/// `autoDispose` všude: krojárku mění i druhá strana předávky, takže se při
/// každém otevření načítá znovu, ne z cache.
final myCostumesProvider = FutureProvider.autoDispose<MyCostumes>(
  (ref) => ref.read(apiClientProvider).getMyCostumes(),
);

final myCostumeHistoryProvider = FutureProvider.autoDispose<List<CostumeTransfer>>(
  (ref) => ref.read(apiClientProvider).getMyCostumeTransfers(),
);

final myCostumeQrProvider = FutureProvider.autoDispose<Uint8List>((ref) async {
  final response = await ref.read(apiClientProvider).getMyCostumeQr();
  return Uint8List.fromList(response.data);
});

final costumeTransferProvider = FutureProvider.autoDispose.family<CostumeTransfer, int>(
  (ref, id) => ref.read(apiClientProvider).getCostumeTransfer(id),
);

/// Klíč fotky: předávka, fotka, náhled/plná velikost.
typedef CostumePhotoKey = ({int transferId, int photoId, bool thumbnail});

final costumePhotoProvider = FutureProvider.autoDispose.family<Uint8List, CostumePhotoKey>((ref, key) async {
  final response = await ref
      .read(apiClientProvider)
      .getCostumePhoto(key.transferId, key.photoId, key.thumbnail ? 'thumb' : 'full');
  return Uint8List.fromList(response.data);
});

/// Přehled pro správce: kdo co má a otevřené předávky.
class CostumeStoreOverview {
  const CostumeStoreOverview({required this.holders, required this.open});

  final List<CostumeHolderOverview> holders;
  final List<CostumeTransfer> open;
}

final costumeStoreProvider = FutureProvider.autoDispose<CostumeStoreOverview>((ref) async {
  final api = ref.read(apiClientProvider);
  final results = await Future.wait([api.getCostumeHolders(), api.getOpenCostumeTransfers()]);
  return CostumeStoreOverview(
    holders: results[0] as List<CostumeHolderOverview>,
    open: results[1] as List<CostumeTransfer>,
  );
});

/// Po každé změně předávky — obnoví všechny pohledy, které ji ukazují.
void invalidateCostumeViews(WidgetRef ref, {int? transferId}) {
  ref.invalidate(myCostumesProvider);
  ref.invalidate(myCostumeHistoryProvider);
  ref.invalidate(costumeStoreProvider);
  if (transferId != null) ref.invalidate(costumeTransferProvider(transferId));
}
