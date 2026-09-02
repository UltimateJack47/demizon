import 'package:demizon/core/formatting.dart';
import 'package:demizon/core/theme.dart';
import 'package:demizon/models/models.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'gallery_controller.dart';
import 'photo_viewer_screen.dart';

/// Přepis `Pages/GalleryPage.xaml`.
class GalleryScreen extends ConsumerWidget {
  const GalleryScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final photos = ref.watch(galleryProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Fotogalerie')),
      body: RefreshIndicator(
        onRefresh: () => ref.read(galleryProvider.notifier).refresh(),
        child: photos.when(
          data: (items) =>
              items.isEmpty ? const _EmptyGallery() : _PhotoGrid(photos: items),
          loading: () => const Center(child: CircularProgressIndicator()),
          // MAUI chybu polykalo a zobrazilo prázdný stav
          // (`GalleryViewModel.cs:31-34`) — chování zachováno.
          error: (_, __) => const _EmptyGallery(),
        ),
      ),
    );
  }
}

class _PhotoGrid extends StatelessWidget {
  const _PhotoGrid({required this.photos});

  final List<GalleryPhoto> photos;

  @override
  Widget build(BuildContext context) {
    return GridView.builder(
      padding: const EdgeInsets.all(4),
      // MAUI: `GridItemsLayout Span=3`, rozestupy 4, výška položky 120.
      gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
        crossAxisCount: 3,
        crossAxisSpacing: 4,
        mainAxisSpacing: 4,
      ),
      itemCount: photos.length,
      itemBuilder: (context, index) {
        final photo = photos[index];
        return GestureDetector(
          onTap: () => context.pushPhotoViewer(
            photos: photos,
            initialIndex: index,
          ),
          child: ClipRRect(
            borderRadius: BorderRadius.circular(4),
            child: Image.network(
              thumbnailUrl(photo.id),
              fit: BoxFit.cover,
              errorBuilder: (_, __, ___) => const ColoredBox(
                color: DemizonColors.warmBeige,
                child: Center(child: Icon(Icons.broken_image_outlined)),
              ),
            ),
          ),
        );
      },
    );
  }
}

class _EmptyGallery extends StatelessWidget {
  const _EmptyGallery();

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) => SingleChildScrollView(
        physics: const AlwaysScrollableScrollPhysics(),
        child: ConstrainedBox(
          constraints: BoxConstraints(minHeight: constraints.maxHeight),
          child: const Center(
            child: Padding(
              padding: EdgeInsets.all(20),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text('📷', style: TextStyle(fontSize: 48)),
                  SizedBox(height: 8),
                  Text(
                    'Žádné fotografie',
                    style: TextStyle(
                      fontSize: 22,
                      fontWeight: FontWeight.bold,
                      color: DemizonColors.textPrimary,
                    ),
                  ),
                  SizedBox(height: 8),
                  Text(
                    'Zatím nebyly přidány žádné veřejné fotografie.',
                    textAlign: TextAlign.center,
                    style: TextStyle(color: DemizonColors.textSecondary),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
