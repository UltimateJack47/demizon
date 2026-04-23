using Demizon.Contracts.Attendances;
using Demizon.Contracts.Dances;
using Demizon.Contracts.Events;
using Demizon.Contracts.Members;

namespace Demizon.Mvc.Mapping;

public static class ContractMappingExtensions
{
    public static EventDto ToDto(this Dal.Entities.Event e, AttendanceDto? myAttendance = null) =>
        new(e.Id, e.Name, e.DateFrom, e.DateTo, e.Place, e.IsCancelled,
            e.Recurrence.ToString(), myAttendance,
            IsPublic: e.IsPublic, NotifyBeforeDays: e.NotifyBeforeDays);

    public static MemberProfileDto ToProfileDto(this Dal.Entities.Member m) =>
        new(m.Id, m.Name, m.Surname, m.Login, m.Email, m.Role.ToString(), m.Gender.ToString());

    public static AttendanceDto ToDto(this Dal.Entities.Attendance a) =>
        new(a.Id, a.Status.ToString().ToLowerInvariant(), a.Comment, a.ActivityRole?.ToString(), a.LastUpdated);

    public static DanceDto ToDto(this Dal.Entities.Dance d) =>
        new(d.Id, d.Name, d.Region, d.Description, d.InternalDescription, d.Lyrics,
            d.Videos.Where(v => v.IsVisible).Select(v => v.ToDto()).ToList());

    public static DanceDocumentDto ToDocumentDto(this Dal.Entities.File f) =>
        new(f.Id, System.IO.Path.GetFileName(f.Path), f.ContentType, f.FileSize);

    public static VideoLinkDto ToDto(this Dal.Entities.VideoLink v) =>
        new(v.Id, v.Name, v.Url, v.Year, v.IsVisible, v.IsInternal);
}
