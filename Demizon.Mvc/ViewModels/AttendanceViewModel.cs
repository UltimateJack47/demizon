using Demizon.Dal.Entities;

namespace Demizon.Mvc.ViewModels;

public class AttendanceViewModel
{
    public int Id { get; set; }

    public AttendanceStatus Status { get; set; } = AttendanceStatus.No;

    public string? Comment { get; set; }

    public AttendanceActivityRole? ActivityRole { get; set; }

    public DateTime Date { get; set; }

    public int? EventId { get; set; }

    public EventViewModel? Event { get; set; }

    public int MemberId { get; set; }

    public MemberViewModel Member { get; set; } = null!;

    public DateTime LastUpdated { get; set; }

    public string? GoogleEventId { get; set; }
}

public static class AttendanceMappingExtensions
{
    public static AttendanceViewModel ToViewModel(this Attendance entity) => new()
    {
        Id = entity.Id,
        Status = entity.Status,
        Comment = entity.Comment,
        ActivityRole = entity.ActivityRole,
        Date = entity.Date,
        EventId = entity.EventId,
        MemberId = entity.MemberId,
        LastUpdated = entity.LastUpdated,
        GoogleEventId = entity.GoogleEventId,
        // Member a Event jsou navigační properties – caller je nastavuje dle potřeby
    };

    public static Attendance ToEntity(this AttendanceViewModel vm) => new()
    {
        Id = vm.Id,
        Status = vm.Status,
        Comment = vm.Comment,
        ActivityRole = vm.ActivityRole,
        Date = vm.Date,
        EventId = vm.EventId,
        MemberId = vm.MemberId,
        LastUpdated = vm.LastUpdated,
        GoogleEventId = vm.GoogleEventId,
    };
}
