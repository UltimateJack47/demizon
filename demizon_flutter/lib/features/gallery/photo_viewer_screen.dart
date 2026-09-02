import 'package:demizon/core/formatting.dart';
import 'package:demizon/core/routes.dart';
import 'package:demizon/models/models.dart';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

/// Parametry prohlížeče. V MAUI se předávaly Shell navigací jako slovník
/// `{ ["Photos"] = ..., ["Index"] = ... }` (`GalleryViewModel.cs:46`);
/// v go_routeru jdou typově jako `extra`.
class PhotoViewerArgs {
  const PhotoViewerArgs({required this.photos, required this.initialIndex});

  final List<GalleryPhoto> photos;
  final int initialIndex;
}

extension PhotoViewerNavigation on BuildContext {
  /// Otevře fullscreen prohlížeč nad daným seznamem fotek.
  void pushPhotoViewer({
    required List<GalleryPhoto> photos,
    required int initialIndex,
  }) =>
      push(
        AppRoutes.photoViewer,
        extra: PhotoViewerArgs(photos: photos, initialIndex: initialIndex),
      );
}

/// Přepis `Pages/PhotoViewerPage.xaml` + `PhotoViewerViewModel.cs`.
///
/// MAUI `CarouselView` + `IndicatorView` odpovídá `PageView` + indikátor.
class PhotoViewerScreen extends StatefulWidget {
  const PhotoViewerScreen({super.key, required this.args});

  /// Router předává `state.extra`, které může chybět (deep link, obnovení
  /// stavu po restartu) — proto nullable.
  final PhotoViewerArgs? args;

  @override
  State<PhotoViewerScreen> createState() => _PhotoViewerScreenState();
}

class _PhotoViewerScreenState extends State<PhotoViewerScreen> {
  late final PageController _controller;
  late int _currentIndex;

  @override
  void initState() {
    super.initState();
    final photos = widget.args?.photos ?? const <GalleryPhoto>[];
    _currentIndex = (widget.args?.initialIndex ?? 0)
        .clamp(0, photos.isEmpty ? 0 : photos.length - 1)
        .toInt();
    _controller = PageController(initialPage: _currentIndex);
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final photos = widget.args?.photos ?? const <GalleryPhoto>[];

    if (photos.isEmpty) {
      return Scaffold(
        backgroundColor: Colors.black,
        appBar: AppBar(
          title: const Text('Fotografie'),
          backgroundColor: Colors.black,
          foregroundColor: Colors.white,
        ),
        body: const Center(
          child: Text(
            'Fotografie nejsou k dispozici.',
            style: TextStyle(color: Colors.white),
          ),
        ),
      );
    }

    return Scaffold(
      backgroundColor: Colors.black,
      appBar: AppBar(
        title: const Text('Fotografie'),
        backgroundColor: Colors.black,
        foregroundColor: Colors.white,
      ),
      body: Stack(
        children: [
          PageView.builder(
            controller: _controller,
            itemCount: photos.length,
            onPageChanged: (index) => setState(() => _currentIndex = index),
            itemBuilder: (context, index) {
              final photo = photos[index];
              return Stack(
                children: [
                  // Vědomé vylepšení proti MAUI: `InteractiveViewer` přidává
                  // pinch-to-zoom a posun. MAUI verze zoom neměla, ve Flutteru
                  // je to jeden widget navíc a uživatel to u fotky očekává.
                  Positioned.fill(
                    child: InteractiveViewer(
                      minScale: 1,
                      maxScale: 4,
                      child: Center(
                        child: Image.network(
                          imageUrl(photo.id),
                          fit: BoxFit.contain,
                          loadingBuilder: (context, child, progress) =>
                              progress == null
                                  ? child
                                  : const Center(
                                      child: CircularProgressIndicator(
                                        color: Colors.white,
                                      ),
                                    ),
                          errorBuilder: (_, __, ___) => const Center(
                            child: Icon(
                              Icons.broken_image_outlined,
                              color: Colors.white54,
                              size: 48,
                            ),
                          ),
                        ),
                      ),
                    ),
                  ),
                  if (photo.danceName != null && photo.danceName!.isNotEmpty)
                    Positioned(
                      left: 0,
                      right: 0,
                      bottom: 60,
                      child: Center(
                        child: Container(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 12,
                            vertical: 8,
                          ),
                          color: const Color(0x80000000),
                          child: Text(
                            photo.danceName!,
                            style: const TextStyle(
                              fontSize: 14,
                              color: Colors.white,
                            ),
                          ),
                        ),
                      ),
                    ),
                ],
              );
            },
          ),
          Positioned(
            left: 0,
            right: 0,
            bottom: 20,
            child: _PageIndicator(
              count: photos.length,
              current: _currentIndex,
            ),
          ),
        ],
      ),
    );
  }
}

/// Protějšek `IndicatorView`. U velkých galerií by se řada teček nevešla,
/// proto se nad limitem přepne na počítadlo.
class _PageIndicator extends StatelessWidget {
  const _PageIndicator({required this.count, required this.current});

  static const _maxDots = 12;

  final int count;
  final int current;

  @override
  Widget build(BuildContext context) {
    if (count <= 1) return const SizedBox.shrink();

    if (count > _maxDots) {
      return Center(
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
          decoration: BoxDecoration(
            color: const Color(0x80000000),
            borderRadius: BorderRadius.circular(12),
          ),
          child: Text(
            '${current + 1} / $count',
            style: const TextStyle(color: Colors.white, fontSize: 13),
          ),
        ),
      );
    }

    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        for (var i = 0; i < count; i++)
          Container(
            width: 8,
            height: 8,
            margin: const EdgeInsets.symmetric(horizontal: 3),
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: i == current ? Colors.white : const Color(0x66FFFFFF),
            ),
          ),
      ],
    );
  }
}
