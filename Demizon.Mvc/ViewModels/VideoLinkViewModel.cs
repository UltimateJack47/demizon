using Demizon.Dal.Entities;

namespace Demizon.Mvc.ViewModels;

public class VideoLinkViewModel
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public bool IsVisible { get; set; } = false;

    public string Url { get; set; } = null!;

    public int Year { get; set; }

    public bool IsInternal { get; set; } = false;

    public int? DanceId { get; set; }

    public DanceViewModel? Dance { get; set; }
}

public static class VideoLinkMappingExtensions
{
    public static VideoLinkViewModel ToViewModel(this VideoLink entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        IsVisible = entity.IsVisible,
        Url = entity.Url,
        Year = entity.Year,
        IsInternal = entity.IsInternal,
        DanceId = entity.DanceId,
    };

    public static VideoLink ToEntity(this VideoLinkViewModel vm) => new()
    {
        Id = vm.Id,
        Name = vm.Name,
        IsVisible = vm.IsVisible,
        Url = vm.Url,
        Year = vm.Year,
        IsInternal = vm.IsInternal,
        DanceId = vm.DanceId,
    };
}
