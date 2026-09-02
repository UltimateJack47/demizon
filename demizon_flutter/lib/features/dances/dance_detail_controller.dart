import 'dart:io';

import 'package:demizon/core/providers.dart';
import 'package:demizon/models/models.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:open_filex/open_filex.dart';
import 'package:path_provider/path_provider.dart';
import 'package:url_launcher/url_launcher.dart';

/// Data detailu tance — protějšek trojice `Dance` / `Photos` / `Documents`
/// z `DanceDetailViewModel`.
class DanceDetail {
  const DanceDetail({
    required this.dance,
    required this.photos,
    required this.documents,
  });

  final Dance dance;
  final List<GalleryPhoto> photos;
  final List<DanceDocument> documents;

  bool get hasPhotos => photos.isNotEmpty;
  bool get hasDocuments => documents.isNotEmpty;
}

final danceDetailProvider =
    AsyncNotifierProvider.family<DanceDetailController, DanceDetail, int>(
  DanceDetailController.new,
);

/// Přepis `ViewModels/DanceDetailViewModel.cs`.
///
/// `IsLyricsExpanded` se sem nepřenáší — rozbalení textu písně řeší
/// `ExpansionTile` na obrazovce sám.
class DanceDetailController extends FamilyAsyncNotifier<DanceDetail, int> {
  @override
  Future<DanceDetail> build(int danceId) => _load(danceId);

  Future<void> refresh() async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(() => _load(arg));
  }

  Future<DanceDetail> _load(int danceId) async {
    final api = ref.read(apiClientProvider);

    // Selhání načtení tance je fatální (MAUI: "Nepodařilo se načíst tanec."),
    // ale fotky a dokumenty se v MAUI tiše polykaly na prázdný seznam
    // (`DanceDetailViewModel.cs:57` a `:68`) — chování zachováno.
    final dance = await api.getDance(danceId);

    List<GalleryPhoto> photos;
    try {
      photos = await api.getDancePhotos(danceId);
    } catch (_) {
      photos = const [];
    }

    List<DanceDocument> documents;
    try {
      documents = await api.getDanceDocuments(danceId);
    } catch (_) {
      documents = const [];
    }

    return DanceDetail(dance: dance, photos: photos, documents: documents);
  }

  /// Otevře video v externí aplikaci (prohlížeč / YouTube).
  /// Vrací chybovou hlášku, nebo `null` při úspěchu.
  Future<String?> openVideo(String url) async {
    final uri = Uri.tryParse(url);
    if (uri == null || !uri.hasScheme || !uri.hasAuthority) {
      return 'Neplatný odkaz na video.';
    }

    try {
      final launched = await launchUrl(
        uri,
        mode: LaunchMode.externalApplication,
      );
      return launched ? null : 'Nepodařilo se otevřít video.';
    } catch (_) {
      return 'Nepodařilo se otevřít video.';
    }
  }

  /// Přepis `Services/DocumentService.DownloadAndOpenAsync`.
  ///
  /// Stáhne dokument do dočasného adresáře pod sanitizovaným názvem
  /// a předá ho systému k otevření. Vrací chybovou hlášku, nebo `null`.
  Future<String?> openDocument(DanceDocument document) async {
    const failure = 'Nepodařilo se otevřít dokument.';

    try {
      final response =
          await ref.read(apiClientProvider).downloadDocument(document.id);

      final status = response.response.statusCode ?? 0;
      if (status < 200 || status >= 300) return failure;

      final bytes = response.data;
      if (bytes.isEmpty) return failure;

      final dir = await getTemporaryDirectory();
      final path = '${dir.path}/${sanitizeFileName(document.fileName)}';
      await File(path).writeAsBytes(bytes, flush: true);

      final result = await OpenFilex.open(path, type: document.contentType);
      return result.type == ResultType.done ? null : failure;
    } catch (_) {
      return failure;
    }
  }
}

/// Protějšek `DocumentService.SanitizeFileName` (`DocumentService.cs:40`).
///
/// C# používalo `Path.GetInvalidFileNameChars()`, které se liší podle
/// platformy. Tady je pevná množina znaků nepovolených na Windows i Androidu,
/// aby se název choval stejně všude.
String sanitizeFileName(String name) {
  final sanitized =
      name.replaceAll(RegExp(r'[\x00-\x1f<>:"/\|?*]'), '_').trim();
  if (sanitized.isEmpty) {
    return 'doc_${DateTime.now().microsecondsSinceEpoch}';
  }
  return sanitized;
}
