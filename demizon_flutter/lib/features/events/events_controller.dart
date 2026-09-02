import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:demizon/core/providers.dart';
import 'package:demizon/models/models.dart';

/// Seznam nadcházejících akcí — protějšek `EventsViewModel`.
///
/// MAUI se překresloval přes `WeakReferenceMessenger` + `IRecipient<EventsChangedMessage>`.
/// Tady message bus není: kdokoli akce mění (detail, založení, editace, smazání)
/// zavolá `ref.invalidate(eventsProvider)` a seznam se načte znovu sám.
final eventsProvider = AsyncNotifierProvider<EventsController, List<Event>>(
  EventsController.new,
);

class EventsController extends AsyncNotifier<List<Event>> {
  @override
  Future<List<Event>> build() => ref.read(apiClientProvider).getUpcomingEvents();

  /// Protějšek `EventsViewModel.LoadAsync` volaného z `RefreshView`.
  Future<void> refresh() async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(
      () => ref.read(apiClientProvider).getUpcomingEvents(),
    );
  }
}
