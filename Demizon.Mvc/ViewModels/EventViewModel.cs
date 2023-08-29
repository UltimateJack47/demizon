using AutoMapper;
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

    public DateRange Date { get; set; } = null!;
    
    public class DtoProfile : Profile
    {
        public DtoProfile()
        {
            CreateMap<Event, EventViewModel>()
                .ReverseMap()
                .ForMember(x => x.DateFrom, opt => opt.MapFrom(y => y.Date.Start!.Value.ToUniversalTime()))
                .ForMember(x => x.DateTo, opt => opt.MapFrom(y => y.Date.End!.Value.ToUniversalTime()));
        }
    }
}
