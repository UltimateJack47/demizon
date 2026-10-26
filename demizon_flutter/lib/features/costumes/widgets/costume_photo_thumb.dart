import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../../core/theme.dart';
import '../../../models/models.dart';
import '../costume_providers.dart';

/// Náhled fotky stavu kusu. Stará fotka (podle EXIF) má oranžový rámeček a ⚠.
class CostumePhotoThumb extends ConsumerWidget {
  const CostumePhotoThumb({required this.transferId, required this.photo, super.key});

  final int transferId;
  final CostumePhoto photo;

  static final _format = DateFormat('d. M. yyyy H:mm');

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final thumb = ref.watch(costumePhotoProvider((transferId: transferId, photoId: photo.id, thumbnail: true)));

    return GestureDetector(
      onTap: () => _openFull(context),
      child: Stack(
        children: [
          Container(
            width: 96,
            height: 96,
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(4),
              border: photo.isStale ? Border.all(color: DemizonColors.warning, width: 3) : null,
            ),
            clipBehavior: Clip.antiAlias,
            child: thumb.when(
              data: (bytes) => Image.memory(bytes, fit: BoxFit.cover),
              loading: () => const Center(child: CircularProgressIndicator(strokeWidth: 2)),
              error: (_, __) => const Icon(Icons.broken_image, color: DemizonColors.textSecondary),
            ),
          ),
          if (photo.isStale)
            const Positioned(
              top: 4,
              right: 4,
              child: Icon(Icons.warning_amber_rounded, color: DemizonColors.warning, size: 20),
            ),
        ],
      ),
    );
  }

  String get _caption {
    final uploaded = _format.format(photo.uploadedAt.toLocal());
    final captured = photo.capturedAt;
    return photo.isStale && captured != null
        ? 'Pozor: podle fotoaparátu pořízená ${_format.format(captured.toLocal())}, nahraná $uploaded.'
        : 'Nahraná $uploaded';
  }

  void _openFull(BuildContext context) {
    showDialog<void>(
      context: context,
      builder: (context) => Dialog(
        child: Consumer(
          builder: (context, ref, _) {
            final full = ref.watch(costumePhotoProvider((transferId: transferId, photoId: photo.id, thumbnail: false)));
            return Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                full.when(
                  data: (bytes) => InteractiveViewer(child: Image.memory(bytes)),
                  loading: () => const Padding(padding: EdgeInsets.all(40), child: CircularProgressIndicator()),
                  error: (_, __) => const Padding(padding: EdgeInsets.all(20), child: Text('Fotku se nepodařilo načíst.')),
                ),
                Padding(padding: const EdgeInsets.all(12), child: Text(_caption, style: const TextStyle(fontSize: 12))),
              ],
            );
          },
        ),
      ),
    );
  }
}
