using AutoMapper;
using Demizon.Dal.Entities;

namespace Demizon.Mvc.ViewModels;

public class DanceViewModel
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public bool IsVisible { get; set; } = false;

    public string? Region { get; set; }

    public string? Description { get; set; }

    public List<VideoLinkViewModel> Videos { get; set; } = [];

    public List<FileViewModel> Files { get; set; } = [];

    public class DtoProfile : Profile
    {
        public DtoProfile()
        {
            CreateMap<Dance, DanceViewModel>()
                .ReverseMap();
        }
    }
}