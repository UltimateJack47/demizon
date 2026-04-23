using Demizon.Dal.Entities;

namespace Demizon.Mvc.ViewModels;

public class DanceViewModel
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public bool IsVisible { get; set; } = false;

    public string? Region { get; set; }

    public string? Description { get; set; }

    public string? InternalDescription { get; set; }

    public string? Lyrics { get; set; }

    public List<VideoLinkViewModel> Videos { get; set; } = [];

    public List<FileViewModel> Files { get; set; } = [];
}

public static class DanceMappingExtensions
{
    public static DanceViewModel ToViewModel(this Dance entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        IsVisible = entity.IsVisible,
        Region = entity.Region,
        Description = entity.Description,
        InternalDescription = entity.InternalDescription,
        Lyrics = entity.Lyrics,
        Videos = entity.Videos?.Select(v => v.ToViewModel()).ToList() ?? [],
        Files = entity.Files?.Select(f => f.ToViewModel()).ToList() ?? [],
    };

    public static Dance ToEntity(this DanceViewModel vm) => new()
    {
        Id = vm.Id,
        Name = vm.Name,
        IsVisible = vm.IsVisible,
        Region = vm.Region,
        Description = vm.Description,
        InternalDescription = vm.InternalDescription,
        Lyrics = vm.Lyrics,
    };
}
