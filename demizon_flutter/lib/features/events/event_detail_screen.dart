import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import 'package:demizon/core/formatting.dart';
import 'package:demizon/core/routes.dart';
import 'package:demizon/core/theme.dart';
import 'package:demizon/models/models.dart';

import 'event_detail_controller.dart';

/// Přepis `Demizon.Maui/Pages/EventDetailPage.xaml`.
///
/// Obrazovka běží ve dvou režimech (viz `EventDetailController`):
/// akce (`eventId`) a zkouška (`rehearsalDate`).
///
/// Cesta ke zkoušce je `/events/0?rehearsalDate=yyyy-MM-dd` (viz
/// `rehearsalDetailPath` v `features/attendance/attendance_controller.dart`),
/// proto se `eventId == 0` s vyplněným datem chápe jako zkouška — stejně jako
/// `EventDetailViewModel.IsRehearsal`.
class EventDetailScreen extends ConsumerStatefulWidget {
  const EventDetailScreen({super.key, this.eventId, this.rehearsalDate});

  final int? eventId;
  final DateTime? rehearsalDate;

  @override
  ConsumerState<EventDetailScreen> createState() => _EventDetailScreenState();
}

class _EventDetailScreenState extends ConsumerState<EventDetailScreen> {
  final _commentController = TextEditingController();
  bool _commentLoaded = false;
  bool _isBusy = false;

  late final EventDetailArgs _args =
      (widget.rehearsalDate != null &&
              (widget.eventId == null || widget.eventId == 0))
          ? EventDetailArgs(rehearsalDate: widget.rehearsalDate)
          : EventDetailArgs(eventId: widget.eventId);

  @override
  void dispose() {
    _commentController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final async = ref.watch(eventDetailProvider(_args));

    // Poznámku držíme v TextEditingController; ze stavu ji převezmeme jen
    // jednou, po prvním načtení, ať uživateli nepřepisujeme rozepsaný text.
    ref.listen(eventDetailProvider(_args), (_, next) {
      final value = next.value;
      if (!_commentLoaded && value != null) {
        _commentLoaded = true;
        _commentController.text = value.comment ?? '';
      }
    });
    final initial = async.value;
    if (!_commentLoaded && initial != null) {
      _commentLoaded = true;
      final text = initial.comment ?? '';
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted) _commentController.text = text;
      });
    }

    return Scaffold(
      appBar: AppBar(title: Text(async.value?.event?.name ?? '')),
      body: async.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (_, __) => const Center(
          child: Padding(
            padding: EdgeInsets.all(20),
            child: Text('Nepodařilo se načíst akci.'),
          ),
        ),
        data: _buildContent,
      ),
    );
  }

  Widget _buildContent(EventDetailState s) {
    final event = s.event;

    return SingleChildScrollView(
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (event != null) _heroCard(event),
          if (s.showAttendees) ...[
            const SizedBox(height: 16),
            _attendeesCard(s.attendees!),
          ],
          const SizedBox(height: 24),
          const Text(
            'Moje docházka',
            style: TextStyle(fontWeight: FontWeight.bold, fontSize: 18),
          ),
          const SizedBox(height: 16),
          _statusButtons(s),
          if (s.isAttending) ...[
            const SizedBox(height: 16),
            _rolePicker(s),
          ],
          const SizedBox(height: 16),
          _commentField(),
          const SizedBox(height: 24),
          FilledButton(
            onPressed: _isBusy ? null : _saveAttendance,
            child: const Text('Uložit docházku'),
          ),
          if (s.isAdmin) ...[
            const SizedBox(height: 24),
            _adminCard(s),
          ],
          const SizedBox(height: 24),
        ],
      ),
    );
  }

  // ── Hero ────────────────────────────────────────────────────────────────

  Widget _heroCard(Event event) {
    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              event.name,
              style: const TextStyle(
                fontSize: 22,
                fontWeight: FontWeight.bold,
                color: DemizonColors.textPrimary,
              ),
            ),
            const SizedBox(height: 8),
            Row(
              children: [
                const Text('📅', style: TextStyle(fontSize: 16)),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    formatDateTimeLong(event.dateFrom),
                    style: const TextStyle(
                      fontSize: 16,
                      color: DemizonColors.textSecondary,
                    ),
                  ),
                ),
              ],
            ),
            if (event.place != null && event.place!.isNotEmpty) ...[
              const SizedBox(height: 8),
              Row(
                children: [
                  const Text('📍', style: TextStyle(fontSize: 16)),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      event.place!,
                      style: const TextStyle(
                        color: DemizonColors.textSecondary,
                      ),
                    ),
                  ),
                ],
              ),
            ],
            if (event.isCancelled) ...[
              const SizedBox(height: 8),
              Align(
                alignment: Alignment.centerLeft,
                child: Container(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                  decoration: BoxDecoration(
                    color: DemizonColors.attendanceNo,
                    borderRadius: BorderRadius.circular(6),
                  ),
                  child: const Text(
                    'ZRUŠENO',
                    style: TextStyle(
                      color: Colors.white,
                      fontWeight: FontWeight.bold,
                      fontSize: 13,
                    ),
                  ),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }

  // ── Kdo přijde ──────────────────────────────────────────────────────────

  Widget _attendeesCard(EventAttendees attendees) {
    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text(
              'Kdo přijde',
              style: TextStyle(fontWeight: FontWeight.bold, fontSize: 18),
            ),
            const SizedBox(height: 12),
            Wrap(
              spacing: 16,
              runSpacing: 4,
              children: [
                _summary('👥', 'Celkem: ${attendees.totalCount}'),
                _summary('💃', 'Tanečníci: ${attendees.dancerCount}'),
                _summary('🎵', 'Muzikanti: ${attendees.musicianCount}'),
              ],
            ),
            const SizedBox(height: 12),
            for (final a in attendees.attendees)
              Padding(
                padding: const EdgeInsets.symmetric(vertical: 4),
                child: Row(
                  children: [
                    const Text(
                      '✓',
                      style: TextStyle(
                        color: DemizonColors.attendanceYes,
                        fontWeight: FontWeight.bold,
                        fontSize: 14,
                      ),
                    ),
                    const SizedBox(width: 8),
                    Flexible(
                      child: Text(
                        a.fullName,
                        style: const TextStyle(fontSize: 14),
                      ),
                    ),
                    const SizedBox(width: 8),
                    // MAUI zobrazoval syrovou API hodnotu ("dancer"/"musician").
                    // TODO(verify): nemá se místo toho použít apiRoleToDisplay?
                    Text(
                      a.activityRole ?? '',
                      style: const TextStyle(
                        color: DemizonColors.textSecondary,
                        fontSize: 12,
                      ),
                    ),
                  ],
                ),
              ),
          ],
        ),
      ),
    );
  }

  Widget _summary(String icon, String text) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(icon, style: const TextStyle(fontSize: 14)),
        const SizedBox(width: 4),
        Text(text, style: const TextStyle(fontSize: 13)),
      ],
    );
  }

  // ── Moje docházka ───────────────────────────────────────────────────────

  Widget _statusButtons(EventDetailState s) {
    return Row(
      children: [
        Expanded(
          child: _statusButton(
            label: '✓ Přijdu',
            value: 'yes',
            color: DemizonTheme.attendanceColor('yes'),
            selected: s.status == 'yes',
          ),
        ),
        const SizedBox(width: 8),
        Expanded(
          child: _statusButton(
            label: '? Nevím',
            value: 'maybe',
            color: DemizonTheme.attendanceColor('maybe'),
            selected: s.status == 'maybe',
          ),
        ),
        const SizedBox(width: 8),
        Expanded(
          child: _statusButton(
            label: '✗ Nepřijdu',
            value: 'no',
            color: DemizonTheme.attendanceColor('no'),
            selected: s.status == 'no',
          ),
        ),
        const SizedBox(width: 8),
        Expanded(
          // Reset — prázdný status znamená smazání záznamu docházky.
          child: _statusButton(
            label: '↺',
            value: '',
            color: DemizonColors.textSecondary,
            selected: false,
            fontSize: 18,
          ),
        ),
      ],
    );
  }

  Widget _statusButton({
    required String label,
    required String value,
    required Color color,
    required bool selected,
    double fontSize = 14,
  }) {
    return SizedBox(
      height: 52,
      child: OutlinedButton(
        onPressed: () =>
            ref.read(eventDetailProvider(_args).notifier).setStatus(value),
        style: OutlinedButton.styleFrom(
          padding: EdgeInsets.zero,
          backgroundColor: selected ? color : Colors.transparent,
          foregroundColor: selected ? Colors.white : color,
          side: BorderSide(color: color, width: 2),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(12),
          ),
        ),
        child: FittedBox(
          fit: BoxFit.scaleDown,
          child: Text(
            label,
            style: TextStyle(
              fontSize: fontSize,
              fontWeight: FontWeight.bold,
            ),
          ),
        ),
      ),
    );
  }

  Widget _rolePicker(EventDetailState s) {
    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: DropdownButtonHideUnderline(
          child: DropdownButton<String>(
            isExpanded: true,
            value: s.activityRole,
            hint: const Text('Vyberte roli'),
            // `roleOptions` = ["Tanečník", "Muzikant"] (EventDetailViewModel.cs:60);
            // na API hodnoty se převádí až při ukládání.
            items: [
              for (final role in roleOptions)
                DropdownMenuItem(value: role, child: Text(role)),
            ],
            onChanged: (value) => ref
                .read(eventDetailProvider(_args).notifier)
                .setActivityRole(value),
          ),
        ),
      ),
    );
  }

  Widget _commentField() {
    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: TextField(
          controller: _commentController,
          minLines: 3,
          maxLines: null,
          keyboardType: TextInputType.multiline,
          decoration: const InputDecoration(
            hintText: 'Poznámka...',
            border: InputBorder.none,
            filled: false,
          ),
        ),
      ),
    );
  }

  // ── Správa (admin) ──────────────────────────────────────────────────────

  Widget _adminCard(EventDetailState s) {
    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const Text(
              'Správa',
              style: TextStyle(
                fontWeight: FontWeight.bold,
                fontSize: 18,
                color: Color(0xFF3E2723),
              ),
            ),
            const SizedBox(height: 12),
            if (!s.isRehearsal)
              FilledButton(
                style: FilledButton.styleFrom(
                  backgroundColor: const Color(0xFFA8845E),
                  foregroundColor: Colors.white,
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(12),
                  ),
                ),
                onPressed: () =>
                    context.push(AppRoutes.eventEditFor(s.args.eventId!)),
                child: const Text(
                  '✏️ Upravit akci',
                  style: TextStyle(fontWeight: FontWeight.bold),
                ),
              ),
            if (s.canSendReminder) ...[
              const SizedBox(height: 12),
              FilledButton(
                style: FilledButton.styleFrom(
                  backgroundColor: const Color(0xFF6B8E4E),
                  foregroundColor: Colors.white,
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(12),
                  ),
                ),
                onPressed: _isBusy ? null : _sendReminder,
                child: const Text(
                  '🔔 Upozornit na docházku',
                  style: TextStyle(fontWeight: FontWeight.bold),
                ),
              ),
            ],
            // TODO(verify): v MAUI se tlačítko zobrazovalo i u zkoušky, kde
            // by ale mazalo akci s id 0. Tady je schované mimo režim akce.
            if (!s.isRehearsal) ...[
              const SizedBox(height: 12),
              OutlinedButton(
                style: OutlinedButton.styleFrom(
                  minimumSize: const Size.fromHeight(48),
                  foregroundColor: const Color(0xFFC0392B),
                  side: const BorderSide(color: Color(0xFFC0392B)),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(12),
                  ),
                ),
                onPressed: _isBusy ? null : _deleteEvent,
                child: const Text(
                  '🗑️ Smazat akci',
                  style: TextStyle(fontWeight: FontWeight.bold),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }

  // ── Akce ────────────────────────────────────────────────────────────────

  Future<void> _saveAttendance() async {
    setState(() => _isBusy = true);
    try {
      await ref
          .read(eventDetailProvider(_args).notifier)
          .saveAttendance(comment: _commentController.text);
      if (!mounted) return;
      context.pop();
    } catch (_) {
      if (!mounted) return;
      await _alert('Chyba', 'Nepodařilo se uložit docházku.');
    } finally {
      if (mounted) setState(() => _isBusy = false);
    }
  }

  Future<void> _sendReminder() async {
    final confirmed = await _confirm(
      title: 'Upozornit na docházku',
      message:
          'Odeslat notifikaci všem členům, kteří nemají vyplněnou docházku?',
      confirmText: 'Odeslat',
    );
    if (!confirmed || !mounted) return;

    setState(() => _isBusy = true);
    try {
      final message =
          await ref.read(eventDetailProvider(_args).notifier).sendReminder();
      if (!mounted) return;
      await _alert('Hotovo', message);
    } catch (e) {
      if (!mounted) return;
      await _alert('Chyba', 'Odeslání selhalo: $e');
    } finally {
      if (mounted) setState(() => _isBusy = false);
    }
  }

  Future<void> _deleteEvent() async {
    final confirmed = await _confirm(
      title: 'Smazat akci',
      message: 'Opravdu chcete smazat tuto akci?',
      confirmText: 'Smazat',
    );
    if (!confirmed || !mounted) return;

    setState(() => _isBusy = true);
    try {
      await ref.read(eventDetailProvider(_args).notifier).deleteEvent();
      if (!mounted) return;
      context.pop();
    } catch (_) {
      if (!mounted) return;
      await _alert('Chyba', 'Nepodařilo se smazat akci.');
    } finally {
      if (mounted) setState(() => _isBusy = false);
    }
  }

  Future<void> _alert(String title, String message) {
    return showDialog<void>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(title),
        content: Text(message),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(),
            child: const Text('OK'),
          ),
        ],
      ),
    );
  }

  Future<bool> _confirm({
    required String title,
    required String message,
    required String confirmText,
  }) async {
    final result = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(title),
        content: Text(message),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: const Text('Zrušit'),
          ),
          TextButton(
            onPressed: () => Navigator.of(context).pop(true),
            child: Text(confirmText),
          ),
        ],
      ),
    );
    return result ?? false;
  }
}
