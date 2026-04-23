namespace Demizon.Dal.Entities;

public class SentNotification
{
    public int Id { get; set; }

    /// <summary>
    /// Null = broadcast (všichni). Jinak konkrétní člen.
    /// </summary>
    public int? MemberId { get; set; }

    public virtual Member? Member { get; set; }

    /// <summary>
    /// Null pro rehearsal notifikace.
    /// </summary>
    public int? EventId { get; set; }

    public virtual Event? Event { get; set; }

    /// <summary>
    /// Pro zkoušky – datum pátku.
    /// </summary>
    public DateTime? RehearsalDate { get; set; }

    public NotificationType NotificationType { get; set; }

    public DateTime SentAt { get; set; }
}

public enum NotificationType
{
    /// <summary>Nová akce – 1h po zadání.</summary>
    NewEvent = 0,

    /// <summary>Připomínka akce – 2 měsíce předem.</summary>
    EventReminder60Days = 1,

    /// <summary>Připomínka akce – 1 měsíc předem.</summary>
    EventReminder30Days = 2,

    /// <summary>Připomínka akce – 2 týdny předem.</summary>
    EventReminder14Days = 3,

    /// <summary>Ruční admin trigger "doplň si docházku". Zabraňuje scheduleru znovu poslat milestone notifikaci stejnému členovi.</summary>
    EventManualReminder = 4,

    /// <summary>Chybí docházka na zkoušku – 5 dní předem.</summary>
    RehearsalReminder5Days = 10,

    /// <summary>Chybí docházka na zkoušku – 3 dny předem.</summary>
    RehearsalReminder3Days = 11,

    /// <summary>Chybí docházka na zkoušku – 1 den předem.</summary>
    RehearsalReminder1Day = 12,
}
