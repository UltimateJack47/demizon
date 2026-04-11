using Demizon.Dal.Entities;

namespace Demizon.Mvc.ViewModels;

public class AttendanceViewModel
{
    public int Id { get; set; }

    public bool Attends { get; set; } = false;

    public string? Comment { get; set; }

    public DateTime Date { get; set; }

    public int? EventId { get; set; }

    public EventViewModel? Event { get; set; }

    public int MemberId { get; set; }

    public MemberViewModel Member { get; set; } = null!;

    public DateTime LastUpdated { get; set; }
}

public static class AttendanceMappingExtensions
{
    public static AttendanceViewModel ToViewModel(this Attendance entity) => new()
    {
        Id = entity.Id,
        Attends = entity.Attends,
        Comment = entity.Comment,
        Date = entity.Date,
        EventId = entity.EventId,
        MemberId = entity.MemberId,
        LastUpdated = entity.LastUpdated,
        // Member a Event jsou navigační properties – caller je nastavuje dle potřeby
    };

    public static Attendance ToEntity(this AttendanceViewModel vm) => new()
    {
        Id = vm.Id,
        Attends = vm.Attends,
        Comment = vm.Comment,
        Date = vm.Date,
        EventId = vm.EventId,
        MemberId = vm.MemberId,
        LastUpdated = vm.LastUpdated,
    };
}
