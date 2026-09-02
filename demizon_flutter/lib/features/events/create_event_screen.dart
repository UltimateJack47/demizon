import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import 'package:demizon/core/formatting.dart';
import 'package:demizon/core/providers.dart';
import 'package:demizon/core/theme.dart';
import 'package:demizon/models/models.dart';

import 'events_controller.dart';

/// Přepis `Demizon.Maui/Pages/CreateEventPage.xaml` + `CreateEventViewModel.cs`.
///
/// Formulář je krátký a nemá žádný stav mimo obrazovku, takže si ho drží
/// sám widget — Riverpod se používá jen pro API klienta a pro invalidaci
/// seznamu akcí po uložení.
class CreateEventScreen extends ConsumerStatefulWidget {
  const CreateEventScreen({super.key});

  @override
  ConsumerState<CreateEventScreen> createState() => _CreateEventScreenState();
}

class _CreateEventScreenState extends ConsumerState<CreateEventScreen> {
  final _nameController = TextEditingController();
  final _placeController = TextEditingController();

  late DateTime _dateFromDate = _today;
  TimeOfDay _dateFromTime = const TimeOfDay(hour: 18, minute: 0);
  late DateTime _dateToDate = _today;
  TimeOfDay _dateToTime = const TimeOfDay(hour: 20, minute: 0);

  bool _isBusy = false;
  String? _errorMessage;

  static DateTime get _today {
    final now = DateTime.now();
    return DateTime(now.year, now.month, now.day);
  }

  DateTime get _dateFrom => _combine(_dateFromDate, _dateFromTime);
  DateTime get _dateTo => _combine(_dateToDate, _dateToTime);

  static DateTime _combine(DateTime date, TimeOfDay time) =>
      DateTime(date.year, date.month, date.day, time.hour, time.minute);

  @override
  void dispose() {
    _nameController.dispose();
    _placeController.dispose();
    super.dispose();
  }

  Future<void> _create() async {
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
      await ref.read(apiClientProvider).createEvent(
            CreateEventRequest(
              name: name.trim(),
              dateFrom: _dateFrom,
              dateTo: _dateTo,
              place: place.isEmpty ? null : place,
              recurrence: 'None',
            ),
          );

      // Protějšek WeakReferenceMessenger.Send(new EventsChangedMessage()).
      ref.invalidate(eventsProvider);
      if (!mounted) return;
      context.pop();
    } catch (_) {
      setState(() => _errorMessage = 'Nepodařilo se vytvořit akci.');
    } finally {
      if (mounted) setState(() => _isBusy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Nová akce')),
      body: SingleChildScrollView(
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
                // MinimumDate="{x:Static sys:DateTime.Today}"
                firstDate: _today,
                onDateChanged: (value) => setState(() {
                  _dateFromDate = value;
                  // CreateEventViewModel.OnDateFromDateChanged
                  if (_dateToDate.isBefore(value)) _dateToDate = value;
                }),
                onTimeChanged: (value) => setState(() => _dateFromTime = value),
              ),
            ),
            const SizedBox(height: 16),
            EventFormField(
              label: 'Konec',
              child: EventDateTimeRow(
                date: _dateToDate,
                time: _dateToTime,
                // MinimumDate="{Binding DateFromDate}"
                firstDate: _dateFromDate,
                onDateChanged: (value) => setState(() => _dateToDate = value),
                onTimeChanged: (value) => setState(() => _dateToTime = value),
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
              onPressed: _isBusy ? null : _create,
              child: const Text('Vytvořit akci'),
            ),
            const SizedBox(height: 20),
            if (_isBusy)
              const Center(child: CircularProgressIndicator()),
          ],
        ),
      ),
    );
  }
}

/// Popisek + karta s obsahem — v XAML `Label(CaptionLabel)` nad `CardBorder`.
/// Sdílí ji i `EditEventScreen`.
class EventFormField extends StatelessWidget {
  const EventFormField({
    super.key,
    required this.label,
    required this.child,
    this.padding = const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
  });

  final String label;
  final Widget child;
  final EdgeInsets padding;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          label,
          style: const TextStyle(
            fontSize: 13,
            color: DemizonColors.textSecondary,
          ),
        ),
        const SizedBox(height: 4),
        Card(
          margin: EdgeInsets.zero,
          child: Padding(padding: padding, child: child),
        ),
      ],
    );
  }
}

/// Dvojice `DatePicker` + `TimePicker` vedle sebe.
class EventDateTimeRow extends StatelessWidget {
  const EventDateTimeRow({
    super.key,
    required this.date,
    required this.time,
    required this.onDateChanged,
    required this.onTimeChanged,
    this.firstDate,
  });

  final DateTime date;
  final TimeOfDay time;
  final ValueChanged<DateTime> onDateChanged;
  final ValueChanged<TimeOfDay> onTimeChanged;
  final DateTime? firstDate;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Expanded(
          child: TextButton(
            onPressed: () async {
              final first = firstDate ?? DateTime(2000);
              final picked = await showDatePicker(
                context: context,
                initialDate: date.isBefore(first) ? first : date,
                firstDate: first,
                lastDate: DateTime(first.year + 5),
              );
              if (picked != null) onDateChanged(picked);
            },
            child: Text(formatDate(date)),
          ),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: TextButton(
            onPressed: () async {
              final picked = await showTimePicker(
                context: context,
                initialTime: time,
              );
              if (picked != null) onTimeChanged(picked);
            },
            // formatTime očekává DateTime, proto skládáme datum + čas.
            child: Text(
              formatTime(
                DateTime(
                  date.year,
                  date.month,
                  date.day,
                  time.hour,
                  time.minute,
                ),
              ),
            ),
          ),
        ),
      ],
    );
  }
}
