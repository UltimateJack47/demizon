using AutoMapper;
using Demizon.Dal.Entities;

namespace Demizon.Mvc.ViewModels;

public class VideoLinkViewModel
{
    public int Id { get; set; }
    
    public string Name { get; set; } = null!;

    public bool IsVisible { get; set; } = false;

    public string Url { get; set; } = null!;

    public int Year { get; set; }

    public class DtoProfile : Profile
    {
        public DtoProfile()
        {
            CreateMap<VideoLink, VideoLinkViewModel>()
                .ReverseMap();
        }
    }
}
