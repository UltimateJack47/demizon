import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import 'package:demizon/core/theme.dart';

import 'member_attendance_detail_controller.dart';

/// Přepis `Pages/Attendance/MemberAttendanceDetailPage.xaml` +
/// `MemberAttendanceDetailViewModel.cs`.
///
/// Admin nastavuje docházku jiného člena: tři stavy, u akcí navíc roli,
/// a poznámku. Zkouška se pozná podle `rehearsalDate` místo `eventId`.
class MemberAttendanceDetailScreen extends ConsumerStatefulWidget {
  const MemberAttendanceDetailScreen({super.key, required this.target});

  final MemberAttendanceTarget target;

  @override
  ConsumerState<MemberAttendanceDetailScreen> createState() =>
      _MemberAttendanceDetailScreenState();
}

class _MemberAttendanceDetailScreenState
    extends ConsumerState<MemberAttendanceDetailScreen> {
  final _commentController = TextEditingController();
  bool _commentSeeded = false;

  @override
  void dispose() {
    _commentController.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    final saved = await ref
        .read(memberAttendanceDetailProvider(widget.target).notifier)
        .save();
    if (!mounted) return;
    if (saved) {
      context.pop();
    } else {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Nepodařilo se uložit docházku.')),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final provider = memberAttendanceDetailProvider(widget.target);
    final async = ref.watch(provider);
    final controller = ref.read(provider.notifier);
    final state = async.valueOrNull;

    // Poznámku načtenou ze serveru nasadíme do pole jen jednou; pak už ji
    // vlastní uživatel.
    if (state != null && !_commentSeeded) {
      _commentSeeded = true;
      _commentController.text = state.comment ?? '';
    }

    return Scaffold(
      appBar: AppBar(title: const Text('Docházka člena')),
      body: async.isLoading
          ? const Center(child: CircularProgressIndicator())
          : async.hasError || state == null
              ? const Center(
                  child: Padding(
                    padding: EdgeInsets.all(20),
                    child: Text(
                      'Nepodařilo se načíst docházku.',
                      style: TextStyle(
                        color: DemizonColors.error,
                        fontSize: 14,
                      ),
                      textAlign: TextAlign.center,
                    ),
                  ),
                )
              : ListView(
                  padding: const EdgeInsets.all(20),
                  children: [
                    _infoCard(state),
                    const SizedBox(height: 16),
                    const Text(
                      'Docházka',
                      style: TextStyle(
                        fontWeight: FontWeight.bold,
                        fontSize: 18,
                      ),
                    ),
                    const SizedBox(height: 16),
                    Row(
                      children: [
                        Expanded(
                          child: _StatusButton(
                            text: '✓ Přijde',
                            color: DemizonTheme.attendanceColor('yes'),
                            selected: state.status == 'yes',
                            onPressed: () => controller.setStatus('yes'),
                          ),
                        ),
                        const SizedBox(width: 8),
                        Expanded(
                          child: _StatusButton(
                            text: '? Nevím',
                            color: DemizonTheme.attendanceColor('maybe'),
                            selected: state.status == 'maybe',
                            onPressed: () => controller.setStatus('maybe'),
                          ),
                        ),
                        const SizedBox(width: 8),
                        Expanded(
                          child: _StatusButton(
                            text: '✗ Nepřijde',
                            color: DemizonTheme.attendanceColor('no'),
                            selected: state.status == 'no',
                            onPressed: () => controller.setStatus('no'),
                          ),
                        ),
                      ],
                    ),
                    // MAUI: ShowRolePicker => IsAttending && !IsRehearsal
                    if (state.isAttending && !widget.target.isRehearsal) ...[
                      const SizedBox(height: 16),
                      Card(
                        margin: EdgeInsets.zero,
                        child: Padding(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 12,
                            vertical: 4,
                          ),
                          child: DropdownButton<String>(
                            isExpanded: true,
                            underline: const SizedBox.shrink(),
                            hint: const Text('Vyberte roli'),
                            value: apiRoleToDisplay(state.activityRole),
                            items: [
                              for (final role in roleOptions)
                                DropdownMenuItem(
                                  value: role,
                                  child: Text(role),
                                ),
                            ],
                            onChanged: (value) => controller
                                .setActivityRole(displayRoleToApi(value)),
                          ),
                        ),
                      ),
                    ],
                    const SizedBox(height: 16),
                    Card(
                      margin: EdgeInsets.zero,
                      child: Padding(
                        padding: const EdgeInsets.all(12),
                        child: TextField(
                          controller: _commentController,
                          onChanged: controller.setComment,
                          maxLines: null,
                          minLines: 4,
                          decoration: const InputDecoration(
                            hintText: 'Poznámka...',
                            border: InputBorder.none,
                            filled: false,
                          ),
                        ),
                      ),
                    ),
                    const SizedBox(height: 24),
                    FilledButton(
                      onPressed: state.isSaving ? null : _save,
                      child: const Text('Uložit docházku'),
                    ),
                    const SizedBox(height: 20),
                  ],
                ),
    );
  }

  Widget _infoCard(MemberAttendanceDetailState state) {
    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                const Text('👤', style: TextStyle(fontSize: 16)),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    widget.target.memberName,
                    style: const TextStyle(
                      fontSize: 20,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 8),
            Row(
              children: [
                const Text('📅', style: TextStyle(fontSize: 16)),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    state.eventName,
                    style: const TextStyle(
                      fontSize: 16,
                      color: DemizonColors.textSecondary,
                    ),
                  ),
                ),
              ],
            ),
            if (state.eventDate != null) ...[
              const SizedBox(height: 8),
              Text(
                // Protějšek DateFormatConverter: "dd. MMMM yyyy, HH:mm" (cs-CZ).
                DateFormat('dd. MMMM yyyy, HH:mm', 'cs')
                    .format(state.eventDate!),
                style: const TextStyle(
                  color: DemizonColors.textSecondary,
                  fontSize: 13,
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

/// Tlačítko stavu: nevybrané = obrys v barvě stavu, vybrané = plná výplň.
/// V MAUI to dělaly tři `DataTrigger`y na každém tlačítku.
class _StatusButton extends StatelessWidget {
  const _StatusButton({
    required this.text,
    required this.color,
    required this.selected,
    required this.onPressed,
  });

  final String text;
  final Color color;
  final bool selected;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 52,
      child: OutlinedButton(
        onPressed: onPressed,
        style: OutlinedButton.styleFrom(
          backgroundColor: selected ? color : Colors.transparent,
          foregroundColor: selected ? DemizonColors.textOnPrimary : color,
          side: BorderSide(color: color, width: 2),
          padding: EdgeInsets.zero,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(12),
          ),
        ),
        child: Text(
          text,
          textAlign: TextAlign.center,
          style: const TextStyle(fontSize: 14, fontWeight: FontWeight.bold),
        ),
      ),
    );
  }
}
