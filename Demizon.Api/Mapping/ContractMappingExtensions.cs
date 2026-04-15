using Demizon.Contracts.Attendances;
using Demizon.Contracts.Dances;
using Demizon.Contracts.Events;

namespace Demizon.Api.Mapping;

public static class ContractMappingExtensions
{
    public static EventDto ToDto(this Dal.Entities.Event e, AttendanceDto? myAttendance = null) =>
        new(e.Id, e.Name, e.DateFrom, e.DateTo, e.Place, e.IsCancelled,
            e.Recurrence.ToString(), myAttendance,
            IsRehearsal: e.Recurrence == Dal.Entities.RecurrenceType.Weekly);

    public static AttendanceDto ToDto(this Dal.Entities.Attendance a) =>
        new(a.Id, a.Attends, a.Comment, a.ActivityRole?.ToString(), a.LastUpdated);

    public static DanceDto ToDto(this Dal.Entities.Dance d) =>
        new(d.Id, d.Name, d.Region, d.Description, d.Lyrics,
            d.Videos.Where(v => v.IsVisible).Select(v => v.ToDto()).ToList());

    public static VideoLinkDto ToDto(this Dal.Entities.VideoLink v) =>
        new(v.Id, v.Name, v.Url, v.Year);
}
