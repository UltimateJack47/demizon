using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Demizon.Maui.ViewModels;

public class EventsChangedMessage : ValueChangedMessage<bool>
{
    public EventsChangedMessage() : base(true) { }
}
