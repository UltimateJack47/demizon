using AutoMapper;
using Demizon.Dal.Entities;

namespace Demizon.Mvc.ViewModels;

public class DanceViewModel
{
    public string Name { get; set; } = null!;

    public bool IsVisible { get; set; } = false;

    public class Read : DanceViewModel
    {
        public int Id { get; set; }
    }
    
    public class Create : DanceViewModel
    {
    }
    
    public class DtoProfile : Profile
    {
        public DtoProfile()
        {
            CreateMap<Dance, Read>()
                .ReverseMap();
            CreateMap<Create, Dance>();
            CreateMap<Read, Create>();
        }
    }
}
