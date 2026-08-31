import 'package:demizon/core/formatting.dart';
import 'package:demizon/core/theme.dart';
import 'package:demizon/features/gallery/photo_viewer_screen.dart';
import 'package:demizon/models/models.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'dance_detail_controller.dart';

/// Přepis `Pages/DanceDetailPage.xaml`.
class DanceDetailScreen extends ConsumerWidget {
  const DanceDetailScreen({super.key, required this.danceId});

  final int danceId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final detail = ref.watch(danceDetailProvider(danceId));

    return Scaffold(
      appBar: AppBar(title: Text(detail.valueOrNull?.dance.name ?? '')),
      body: detail.when(
        data: (data) => _DanceDetailBody(danceId: danceId, detail: data),
        loading: () => const Center(child: CircularProgressIndicator()),
        // MAUI: `ErrorMessage = "Nepodařilo se načíst tanec."`
        // (`DanceDetailViewModel.cs:72`).
        error: (_, __) => const Center(
          child: Padding(
            padding: EdgeInsets.all(20),
            child: Text(
              'Nepodařilo se načíst tanec.',
              style: TextStyle(fontSize: 14, color: DemizonColors.error),
            ),
          ),
        ),
      ),
    );
  }
}

class _DanceDetailBody extends ConsumerWidget {
  const _DanceDetailBody({required this.danceId, required this.detail});

  final int danceId;
  final DanceDetail detail;

  bool _has(String? value) => value != null && value.trim().isNotEmpty;

  Future<void> _openVideo(
    BuildContext context,
    WidgetRef ref,
    String url,
  ) async {
    final error =
        await ref.read(danceDetailProvider(danceId).notifier).openVideo(url);
    if (error != null && context.mounted) {
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(error)));
    }
  }

  Future<void> _openDocument(
    BuildContext context,
    WidgetRef ref,
    DanceDocument document,
  ) async {
    final error = await ref
        .read(danceDetailProvider(danceId).notifier)
        .openDocument(document);
    if (error != null && context.mounted) {
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(error)));
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final dance = detail.dance;

    return RefreshIndicator(
      onRefresh: () => ref.read(danceDetailProvider(danceId).notifier).refresh(),
      child: ListView(
        padding: const EdgeInsets.all(20),
        children: [
          // ----------------------------------------------------------- Hero
          Text(
            dance.name,
            style: const TextStyle(
              fontSize: 22,
              fontWeight: FontWeight.bold,
              color: DemizonColors.textPrimary,
            ),
          ),
          if (_has(dance.region)) ...[
            const SizedBox(height: 8),
            Align(
              alignment: Alignment.centerLeft,
              child: Container(
                padding:
                    const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                decoration: BoxDecoration(
                  color: DemizonColors.primaryDark,
                  borderRadius: BorderRadius.circular(12),
                ),
                child: Text(
                  dance.region!,
                  style: const TextStyle(fontSize: 13, color: Colors.white),
                ),
              ),
            ),
          ],

          // ---------------------------------------------- Popis (veřejný)
          if (_has(dance.description)) ...[
            const _SectionDivider(),
            Text(
              dance.description!,
              style: const TextStyle(
                fontSize: 15,
                height: 1.4,
                color: DemizonColors.textPrimary,
              ),
            ),
          ],

          // ------------------------------------- Interní popis (jen členi)
          if (_has(dance.internalDescription)) ...[
            const _SectionDivider(),
            Row(
              children: [
                const _SectionTitle('Interní popis'),
                const SizedBox(width: 6),
                Container(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                  decoration: BoxDecoration(
                    color: DemizonColors.warmBeige,
                    borderRadius: BorderRadius.circular(8),
                  ),
                  child: const Text(
                    'jen pro členy',
                    style: TextStyle(
                      fontSize: 11,
                      color: DemizonColors.textSecondary,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 6),
            Text(
              dance.internalDescription!,
              style: const TextStyle(
                fontSize: 15,
                height: 1.4,
                color: DemizonColors.textPrimary,
              ),
            ),
          ],

          // ---------------------------------------- Text písně (rozbalovací)
          if (_has(dance.lyrics)) ...[
            const _SectionDivider(),
            Theme(
              // ExpansionTile nahrazuje ruční `IsLyricsExpanded` + tlačítko
              // "Zobrazit"/"Skrýt" z MAUI.
              data:
                  Theme.of(context).copyWith(dividerColor: Colors.transparent),
              child: ExpansionTile(
                tilePadding: EdgeInsets.zero,
                childrenPadding: const EdgeInsets.only(bottom: 8),
                expandedCrossAxisAlignment: CrossAxisAlignment.start,
                title: const _SectionTitle('Text písně'),
                children: [
                  Text(
                    dance.lyrics!,
                    style: const TextStyle(
                      fontSize: 15,
                      height: 1.5,
                      color: DemizonColors.textPrimary,
                    ),
                  ),
                ],
              ),
            ),
          ],

          // ---------------------------------------------------------- Videa
          const _SectionDivider(),
          const _SectionTitle('Videa'),
          const SizedBox(height: 10),
          if (dance.videos.isEmpty)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 12),
              child: Center(
                child: Text(
                  'Žádná videa',
                  style: TextStyle(
                    fontSize: 13,
                    color: DemizonColors.textSecondary,
                  ),
                ),
              ),
            )
          else
            for (final video in dance.videos)
              _VideoCard(
                video: video,
                onTap: () => _openVideo(context, ref, video.url),
              ),

          // ---------------------------------------------------- Fotogalerie
          if (detail.hasPhotos) ...[
            const _SectionDivider(),
            const _SectionTitle('Fotogalerie'),
            const SizedBox(height: 10),
            GridView.builder(
              shrinkWrap: true,
              physics: const NeverScrollableScrollPhysics(),
              gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
                crossAxisCount: 3,
                crossAxisSpacing: 4,
                mainAxisSpacing: 4,
              ),
              itemCount: detail.photos.length,
              itemBuilder: (context, index) {
                final photo = detail.photos[index];
                return GestureDetector(
                  onTap: () => context.pushPhotoViewer(
                    photos: detail.photos,
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
            ),
          ],

          // ------------------------------------------ Dokumenty (jen členi)
          if (detail.hasDocuments) ...[
            const _SectionDivider(),
            const _SectionTitle('Dokumenty'),
            const SizedBox(height: 10),
            for (final document in detail.documents)
              _DocumentCard(
                document: document,
                onTap: () => _openDocument(context, ref, document),
              ),
          ],
        ],
      ),
    );
  }
}

class _VideoCard extends StatelessWidget {
  const _VideoCard({required this.video, required this.onTap});

  final VideoLink video;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: const EdgeInsets.symmetric(vertical: 4),
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: Row(
            children: [
              const Text(
                '▶',
                style: TextStyle(fontSize: 20, color: DemizonColors.primary),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      video.name,
                      style: const TextStyle(
                        fontSize: 15,
                        fontWeight: FontWeight.bold,
                        color: DemizonColors.textPrimary,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 8,
                        vertical: 3,
                      ),
                      decoration: BoxDecoration(
                        color: DemizonColors.warmBeige,
                        borderRadius: BorderRadius.circular(8),
                      ),
                      child: Text(
                        '${video.year}',
                        style: const TextStyle(
                          fontSize: 12,
                          color: DemizonColors.textSecondary,
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _DocumentCard extends StatelessWidget {
  const _DocumentCard({required this.document, required this.onTap});

  final DanceDocument document;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: const EdgeInsets.symmetric(vertical: 4),
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: Row(
            children: [
              const Text('📄', style: TextStyle(fontSize: 22)),
              const SizedBox(width: 10),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      document.fileName,
                      style: const TextStyle(
                        fontSize: 14,
                        fontWeight: FontWeight.bold,
                        color: DemizonColors.textPrimary,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      document.contentType,
                      style: const TextStyle(
                        fontSize: 11,
                        color: DemizonColors.textSecondary,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _SectionDivider extends StatelessWidget {
  const _SectionDivider();

  @override
  Widget build(BuildContext context) => const Padding(
        padding: EdgeInsets.symmetric(vertical: 16),
        child: Divider(height: 1, thickness: 1),
      );
}

class _SectionTitle extends StatelessWidget {
  const _SectionTitle(this.text);

  final String text;

  @override
  Widget build(BuildContext context) => Text(
        text,
        style: const TextStyle(
          fontSize: 16,
          fontWeight: FontWeight.bold,
          color: DemizonColors.textPrimary,
        ),
      );
}
