import 'package:flutter/material.dart';

import '../../../core/theme.dart';
import '../../../models/models.dart';

/// Kus nabídnutý k výběru. `holder` je u příjmu ten, kdo kus vrací.
class CostumePickEntry {
  const CostumePickEntry({required this.item, required this.count, this.holder});

  final CostumeItem item;

  /// Kolik kusů je k dispozici (u šarže víc než 1).
  final int count;
  final CostumeParty? holder;
}

/// Výběr kusu ze seznamu místo skenu — štítek se nedá naskenovat, nebo je kusů
/// hodně. Vybraný kus jde stejnou kontrolou jako naskenovaný.
Future<CostumePickEntry?> showCostumeItemPicker(BuildContext context, List<CostumePickEntry> entries) {
  return showModalBottomSheet<CostumePickEntry>(
    context: context,
    isScrollControlled: true,
    showDragHandle: true,
    builder: (context) => _Picker(entries: entries),
  );
}

class _Picker extends StatefulWidget {
  const _Picker({required this.entries});

  final List<CostumePickEntry> entries;

  @override
  State<_Picker> createState() => _PickerState();
}

class _PickerState extends State<_Picker> {
  String _filter = '';

  bool _matches(CostumePickEntry e) {
    if (_filter.isEmpty) return true;
    final text = '${e.item.code} ${e.item.description} ${e.holder?.fullName ?? ''}'.toLowerCase();
    return _filter.split(' ').where((w) => w.isNotEmpty).every(text.contains);
  }

  @override
  Widget build(BuildContext context) {
    final visible = widget.entries.where(_matches).toList();
    return SizedBox(
      height: MediaQuery.of(context).size.height * 0.75,
      child: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
            child: TextField(
              decoration: const InputDecoration(
                prefixIcon: Icon(Icons.search),
                hintText: 'Hledat kód, součástku, velikost, člena…',
                isDense: true,
              ),
              onChanged: (v) => setState(() => _filter = v.trim().toLowerCase()),
            ),
          ),
          Expanded(
            child: visible.isEmpty
                ? Center(
                    child: Text(widget.entries.isEmpty ? 'Žádné kusy k výběru.' : 'Nic neodpovídá hledání.',
                        style: TextStyle(color: context.palette.textSecondary)),
                  )
                : ListView.builder(
                    itemCount: visible.length,
                    itemBuilder: (context, i) {
                      final e = visible[i];
                      return ListTile(
                        title: Text('${e.item.code}  ${e.item.description}'),
                        subtitle: Text([
                          if (e.holder != null) e.holder!.fullName,
                          if (e.item.color != null) e.item.color!,
                          if (e.item.isBulk) '${e.count} ks',
                        ].join(' · ')),
                        onTap: () => Navigator.pop(context, e),
                      );
                    },
                  ),
          ),
        ],
      ),
    );
  }
}
