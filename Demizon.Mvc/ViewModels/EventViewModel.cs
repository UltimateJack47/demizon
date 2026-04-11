using Demizon.Dal.Entities;
using MudBlazor;

namespace Demizon.Mvc.ViewModels;

public class EventViewModel
{
    public int Id { get; set; }

    public DateTime DateFrom { get; set; }

    public DateTime DateTo { get; set; }

    public string Name { get; set; } = null!;

    public string? Place { get; set; }

    public string? Information { get; set; }

    public DateRange Date { get; set; } = null!;

    public bool IsPublic { get; set; }

    public bool IsCancelled { get; set; }

    public int? NotifyBeforeDays { get; set; }

    public List<AttendanceViewModel> Attendances { get; set; } = [];
}

public static class EventMappingExtensions
{
    public static EventViewModel ToViewModel(this Event entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Place = entity.Place,
        Information = entity.Information,
        IsPublic = entity.IsPublic,
        IsCancelled = entity.IsCancelled,
        NotifyBeforeDays = entity.NotifyBeforeDays,
        DateFrom = entity.DateFrom,
        DateTo = entity.DateTo,
        Date = new DateRange(entity.DateFrom, entity.DateTo),
    };

    public static Event ToEntity(this EventViewModel vm) => new()
    {
        Id = vm.Id,
        Name = vm.Name,
        Place = vm.Place,
        Information = vm.Information,
        IsPublic = vm.IsPublic,
        IsCancelled = vm.IsCancelled,
        NotifyBeforeDays = vm.NotifyBeforeDays,
        DateFrom = vm.Date.Start!.Value,
        DateTo = vm.Date.End!.Value,
    };
}
