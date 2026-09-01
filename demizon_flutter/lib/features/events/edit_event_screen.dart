import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import 'package:demizon/core/providers.dart';
import 'package:demizon/core/theme.dart';
import 'package:demizon/models/models.dart';

import 'create_event_screen.dart' show EventFormField, EventDateTimeRow;
import 'event_detail_controller.dart';
import 'events_controller.dart';

/// Přepis `Demizon.Maui/Pages/EditEventPage.xaml` + `EditEventViewModel.cs`.
class EditEventScreen extends ConsumerStatefulWidget {
  const EditEventScreen({super.key, required this.eventId});

  final int eventId;

  @override
  ConsumerState<EditEventScreen> createState() => _EditEventScreenState();
}

class _EditEventScreenState extends ConsumerState<EditEventScreen> {
  final _nameController = TextEditingController();
  final _placeController = TextEditingController();

  DateTime _dateFromDate = DateTime.now();
  TimeOfDay _dateFromTime = const TimeOfDay(hour: 18, minute: 0);
  DateTime _dateToDate = DateTime.now();
  TimeOfDay _dateToTime = const TimeOfDay(hour: 20, minute: 0);

  bool _isPublic = false;
  bool _isCancelled = false;

  bool _isLoading = true;
  bool _isBusy = false;
  String? _errorMessage;

  DateTime get _dateFrom => _combine(_dateFromDate, _dateFromTime);
  DateTime get _dateTo => _combine(_dateToDate, _dateToTime);

  static DateTime _combine(DateTime date, TimeOfDay time) =>
      DateTime(date.year, date.month, date.day, time.hour, time.minute);

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _nameController.dispose();
    _placeController.dispose();
    super.dispose();
  }

  /// `EditEventViewModel.LoadAsync`
  Future<void> _load() async {
    try {
      final ev = await ref.read(apiClientProvider).getEvent(widget.eventId);
      if (!mounted) return;
      setState(() {
        _nameController.text = ev.name;
        _placeController.text = ev.place ?? '';
        _dateFromDate =
            DateTime(ev.dateFrom.year, ev.dateFrom.month, ev.dateFrom.day);
        _dateFromTime = TimeOfDay.fromDateTime(ev.dateFrom);
        _dateToDate = DateTime(ev.dateTo.year, ev.dateTo.month, ev.dateTo.day);
        _dateToTime = TimeOfDay.fromDateTime(ev.dateTo);
        _isPublic = ev.isPublic;
        _isCancelled = ev.isCancelled;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() => _errorMessage = 'Nepodařilo se načíst akci.');
    } finally {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  /// `EditEventViewModel.SaveAsync`
  Future<void> _save() async {
    setState(() => _errorMessage = null);

    final name = _nameController.text;
    if (name.trim().isEmpty) {
      setState(() => _errorMessage = 'Název akce je povinný.');
      return;
    }

    if (!_dateFrom.isBefore(_dateTo)) {
      setState(
        () => _errorMessage = 'Datum začátku musí být před datem konce.',
      );
      return;
    }

    setState(() => _isBusy = true);
    try {
      final place = _placeController.text.trim();
      await ref.read(apiClientProvider).updateEvent(
            widget.eventId,
            UpdateEventRequest(
              name: name.trim(),
              dateFrom: _dateFrom,
              dateTo: _dateTo,
              place: place.isEmpty ? null : place,
              recurrence: 'None',
              isPublic: _isPublic,
              isCancelled: _isCancelled,
            ),
          );

      _invalidateEvent();
      if (!mounted) return;
      context.pop();
    } catch (_) {
      setState(() => _errorMessage = 'Nepodařilo se uložit akci.');
    } finally {
      if (mounted) setState(() => _isBusy = false);
    }
  }

  /// `EditEventViewModel.DeleteAsync` — s potvrzovacím dialogem.
  Future<void> _delete() async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Smazat akci'),
        content: const Text('Opravdu chcete smazat tuto akci?'),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: const Text('Zrušit'),
          ),
          TextButton(
            onPressed: () => Navigator.of(context).pop(true),
            child: const Text('Smazat'),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) return;

    setState(() => _isBusy = true);
    try {
      await ref.read(apiClientProvider).deleteEvent(widget.eventId);
      _invalidateEvent();
      if (!mounted) return;
      context.pop();
    } catch (_) {
      setState(() => _errorMessage = 'Nepodařilo se smazat akci.');
    } finally {
      if (mounted) setState(() => _isBusy = false);
    }
  }

  /// Protějšek `WeakReferenceMessenger.Send(new EventsChangedMessage())` —
  /// překreslí seznam akcí i detail, ze kterého se sem uživatel dostal.
  void _invalidateEvent() {
    ref.invalidate(eventsProvider);
    ref.invalidate(
      eventDetailProvider(EventDetailArgs(eventId: widget.eventId)),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Upravit akci')),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator())
          : SingleChildScrollView(
              padding: const EdgeInsets.all(20),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  EventFormField(
                    label: 'Název akce',
                    child: TextField(
                      controller: _nameController,
                      enabled: !_isBusy,
                      decoration: const InputDecoration(
                        hintText: 'Např. Zkoušky, Vystoupení...',
                        border: InputBorder.none,
                        filled: false,
                      ),
                    ),
                  ),
                  const SizedBox(height: 16),
                  EventFormField(
                    label: 'Začátek',
                    child: EventDateTimeRow(
                      date: _dateFromDate,
                      time: _dateFromTime,
                      // U editace DatePicker minimum nemá (akce může být v minulosti).
                      onDateChanged: (value) => setState(() {
                        _dateFromDate = value;
                        // EditEventViewModel.OnDateFromDateChanged
                        if (_dateToDate.isBefore(value)) _dateToDate = value;
                      }),
                      onTimeChanged: (value) =>
                          setState(() => _dateFromTime = value),
                    ),
                  ),
                  const SizedBox(height: 16),
                  EventFormField(
                    label: 'Konec',
                    child: EventDateTimeRow(
                      date: _dateToDate,
                      time: _dateToTime,
                      firstDate: _dateFromDate,
                      onDateChanged: (value) =>
                          setState(() => _dateToDate = value),
                      onTimeChanged: (value) =>
                          setState(() => _dateToTime = value),
                    ),
                  ),
                  const SizedBox(height: 16),
                  EventFormField(
                    label: 'Místo (volitelné)',
                    child: TextField(
                      controller: _placeController,
                      enabled: !_isBusy,
                      decoration: const InputDecoration(
                        hintText: 'Např. Sokolovna, Sál...',
                        border: InputBorder.none,
                        filled: false,
                      ),
                    ),
                  ),
                  const SizedBox(height: 16),
                  _toggleCard(
                    label: 'Veřejná akce',
                    value: _isPublic,
                    activeColor: const Color(0xFFA8845E),
                    onChanged: (value) => setState(() => _isPublic = value),
                  ),
                  const SizedBox(height: 16),
                  _toggleCard(
                    label: 'Zrušená akce',
                    value: _isCancelled,
                    activeColor: DemizonColors.attendanceNo,
                    onChanged: (value) => setState(() => _isCancelled = value),
                  ),
                  if (_errorMessage != null) ...[
                    const SizedBox(height: 16),
                    Text(
                      _errorMessage!,
                      style: const TextStyle(
                        color: DemizonColors.error,
                        fontSize: 14,
                      ),
                    ),
                  ],
                  const SizedBox(height: 24),
                  FilledButton(
                    onPressed: _isBusy ? null : _save,
                    child: const Text('Uložit změny'),
                  ),
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
                    onPressed: _isBusy ? null : _delete,
                    child: const Text(
                      'Smazat akci',
                      style: TextStyle(fontWeight: FontWeight.bold),
                    ),
                  ),
                  const SizedBox(height: 20),
                  if (_isBusy) const Center(child: CircularProgressIndicator()),
                ],
              ),
            ),
    );
  }

  Widget _toggleCard({
    required String label,
    required bool value,
    required Color activeColor,
    required ValueChanged<bool> onChanged,
  }) {
    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Row(
          children: [
            Expanded(
              child: Text(
                label,
                style: const TextStyle(color: DemizonColors.textSecondary),
              ),
            ),
            Switch(
              value: value,
              activeThumbColor: activeColor,
              onChanged: _isBusy ? null : onChanged,
            ),
          ],
        ),
      ),
    );
  }
}
