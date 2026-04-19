using File = Demizon.Dal.Entities.File;

namespace Demizon.Mvc.ViewModels;

public class FileViewModel
{
    public int Id { get; set; }

    public MemberViewModel? Member { get; set; }

    public int? MemberId { get; set; }

    public string Path { get; set; } = null!;

    public string FileExtension { get; set; } = null!;

    public string ContentType { get; set; } = null!;

    public long FileSize { get; set; }

    public int? DanceId { get; set; }

    public DanceViewModel? Dance { get; set; }

    public bool IsPublic { get; set; }

    public bool HasDbData { get; set; }
}

public static class FileMappingExtensions
{
    public static FileViewModel ToViewModel(this File entity) => new()
    {
        Id = entity.Id,
        Path = entity.Path,
        FileExtension = entity.FileExtension,
        ContentType = entity.ContentType,
        FileSize = entity.FileSize,
        MemberId = entity.MemberId,
        DanceId = entity.DanceId,
        IsPublic = entity.IsPublic,
        HasDbData = entity.Data != null,
    };

    public static File ToEntity(this FileViewModel vm) => new()
    {
        Id = vm.Id,
        Path = vm.Path,
        FileExtension = vm.FileExtension,
        ContentType = vm.ContentType,
        FileSize = vm.FileSize,
        MemberId = vm.MemberId,
        DanceId = vm.DanceId,
        IsPublic = vm.IsPublic,
    };
}
