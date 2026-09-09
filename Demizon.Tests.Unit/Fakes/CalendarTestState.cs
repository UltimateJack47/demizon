namespace Demizon.Tests.Unit.Fakes;

/// <summary>
/// Sdílený stav dvojníků kalendáře a docházky: co se má chovat jak a co se
/// skutečně stalo. Drží se jako singleton v hostu, aby na něj test dosáhl.
/// </summary>
public sealed class CalendarTestState
{
    private readonly object _gate = new();
    private readonly List<string> _created = [];
    private readonly List<string> _deleted = [];

    // ------------------------------------------------------------- chování

    /// <summary>ID, které vrátí <c>CreateEventAsync</c>. Null = simuluje selhání.</summary>
    public string? CreatedEventId { get; set; } = "gcal-event-1";

    /// <summary>Google odvolal token — služba na to hází vlastní výjimku.</summary>
    public bool ThrowTokenRevoked { get; set; }

    /// <summary>
    /// <c>CreateOrUpdateAsync</c> a <c>DeleteAsync</c> vrátí neúspěch.
    /// Tím se vynutí právě ten stav, na který kompenzační logika reaguje:
    /// v kalendáři se něco stalo, ale do databáze se to nezapsalo.
    /// </summary>
    public bool FailAttendanceWrites { get; set; }

    /// <summary>Selže i samotné dorovnání, aby šlo ověřit, že to nespadne.</summary>
    public bool FailClearGoogleEventId { get; set; }

    // ------------------------------------------------------------- záznam

    public IReadOnlyList<string> CreatedEvents { get { lock (_gate) return _created.ToList(); } }

    public IReadOnlyList<string> DeletedEvents { get { lock (_gate) return _deleted.ToList(); } }

    public int ClearGoogleEventIdCalls { get; private set; }

    public void RecordCreated(string id) { lock (_gate) _created.Add(id); }

    public void RecordDeleted(string id) { lock (_gate) _deleted.Add(id); }

    public void RecordClearCall() { lock (_gate) ClearGoogleEventIdCalls++; }

    public void Reset()
    {
        lock (_gate)
        {
            _created.Clear();
            _deleted.Clear();
            ClearGoogleEventIdCalls = 0;
        }

        CreatedEventId = "gcal-event-1";
        ThrowTokenRevoked = false;
        FailAttendanceWrites = false;
        FailClearGoogleEventId = false;
    }
}
