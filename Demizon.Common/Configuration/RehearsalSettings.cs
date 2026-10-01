namespace Demizon.Common.Configuration;

/// <summary>
/// Pravidelná zkouška. Sekce <c>Rehearsals</c> v appsettings. Časová zóna se bere
/// z <see cref="GoogleCalendarSettings.TimeZone"/>.
/// </summary>
public class RehearsalSettings
{
    public DayOfWeek DayOfWeek { get; set; } = DayOfWeek.Friday;

    /// <summary>Začátek v místním čase.</summary>
    public TimeSpan StartTime { get; set; } = new(19, 0, 0);

    public TimeSpan Duration { get; set; } = new(2, 30, 0);
}
