import 'package:demizon/core/providers.dart';
import 'package:demizon/models/models.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Veřejná fotogalerie — protějšek `GalleryViewModel.LoadPhotosAsync`
/// (`GalleryViewModel.cs:16`).
///
/// MAUI si k fotce dopočítávalo `ThumbnailUrl` / `FullUrl` do vlastní třídy
/// `GalleryPhotoItem`. Tady zůstává čistý model `GalleryPhoto` a URL staví
/// helpery `thumbnailUrl` / `imageUrl` z `core/formatting.dart`.
///
/// `IsEmpty` z MAUI se nepřenáší — prázdnost je `photos.isEmpty` a chyba
/// se řeší v `AsyncValue`.
final galleryProvider =
    AsyncNotifierProvider<GalleryController, List<GalleryPhoto>>(
  GalleryController.new,
);

class GalleryController extends AsyncNotifier<List<GalleryPhoto>> {
  @override
  Future<List<GalleryPhoto>> build() =>
      ref.read(apiClientProvider).getGalleryPhotos();

  /// MAUI načítalo galerii při každém `OnAppearing`. Riverpod si výsledek
  /// drží, takže znovunačtení je na uživateli (pull-to-refresh).
  Future<void> refresh() async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(
      () => ref.read(apiClientProvider).getGalleryPhotos(),
    );
  }
}
