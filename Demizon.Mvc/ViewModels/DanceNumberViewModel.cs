using System.ComponentModel.DataAnnotations;
using Demizon.Dal.Entities;

namespace Demizon.Mvc.ViewModels;

public class DanceNumberViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Název je povinný.")]
    [StringLength(200, ErrorMessage = "Název může mít maximálně 200 znaků.")]
    public string Title { get; set; } = null!;

    [StringLength(2000, ErrorMessage = "Popis může mít maximálně 2000 znaků.")]
    public string? Description { get; set; }

    [StringLength(5000, ErrorMessage = "Text písně může mít maximálně 5000 znaků.")]
    public string? Lyrics { get; set; }

    public int DanceId { get; set; }
}

public static class DanceNumberMappingExtensions
{
    public static DanceNumberViewModel ToViewModel(this DanceNumber entity) => new()
    {
        Id = entity.Id,
        Title = entity.Title,
        Description = entity.Description,
        Lyrics = entity.Lyrics,
        DanceId = entity.DanceId,
    };

    public static DanceNumber ToEntity(this DanceNumberViewModel vm) => new()
    {
        Id = vm.Id,
        Title = vm.Title,
        Description = vm.Description,
        Lyrics = vm.Lyrics,
        DanceId = vm.DanceId,
    };
}
