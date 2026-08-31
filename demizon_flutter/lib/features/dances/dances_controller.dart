import 'package:demizon/core/providers.dart';
import 'package:demizon/models/models.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Seznam tanců — protějšek `DancesViewModel.LoadAsync` (`DancesViewModel.cs:28`).
///
/// MAUI si drželo dvě kolekce (`_allDances` + `FilteredDances`) a po každé
/// změně textu přepočítalo filtr ručně. Tady drží controller jen zdrojová
/// data a filtr je odvozený provider — stejné chování, bez ruční synchronizace.
final dancesProvider = AsyncNotifierProvider<DancesController, List<Dance>>(
  DancesController.new,
);

class DancesController extends AsyncNotifier<List<Dance>> {
  @override
  Future<List<Dance>> build() => ref.read(apiClientProvider).getDances();

  Future<void> refresh() async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(
      () => ref.read(apiClientProvider).getDances(),
    );
  }
}

/// Text ve vyhledávacím poli. Protějšek `DancesViewModel.SearchText`.
final danceSearchProvider = StateProvider<String>((ref) => '');

/// Klientský filtr — protějšek `DancesViewModel.ApplyFilter` (`:58`).
///
/// Hledá se v názvu a v regionu, bez ohledu na velikost písmen
/// (C# používal `StringComparison.OrdinalIgnoreCase`).
final filteredDancesProvider = Provider<AsyncValue<List<Dance>>>((ref) {
  final search = ref.watch(danceSearchProvider).trim().toLowerCase();

  return ref.watch(dancesProvider).whenData((dances) {
    if (search.isEmpty) return dances;
    return dances.where((d) {
      final name = d.name.toLowerCase();
      final region = d.region?.toLowerCase() ?? '';
      return name.contains(search) || region.contains(search);
    }).toList();
  });
});
