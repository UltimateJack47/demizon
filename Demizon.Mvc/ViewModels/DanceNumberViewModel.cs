using AutoMapper;

namespace Demizon.Mvc.ViewModels;

public class DanceNumberViewModel
{
    public int Id { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

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
