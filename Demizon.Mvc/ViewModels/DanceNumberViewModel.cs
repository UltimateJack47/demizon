using System.ComponentModel.DataAnnotations;
using AutoMapper;

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

    public class DtoProfile : Profile
    {
        public DtoProfile()
        {
            CreateMap<Dal.Entities.DanceNumber, DanceNumberViewModel>().ReverseMap();
        }
    }
}
