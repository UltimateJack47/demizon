using AutoMapper;
using Demizon.Dal.Entities;
using MudBlazor;

namespace Demizon.Mvc.ViewModels;

public class EventViewModel
{
    public string Name { get; set; } = null!;

    public string? Place { get; set; }
    

    public class Read : EventViewModel
    {
        public int Id { get; set; }
        
        public DateTime DateFrom { get; set; }
        
        public DateTime DateTo { get; set; }
    }

    public class Create : EventViewModel
    {
        public DateRange Date { get; set; } = null!;
    }

    public class DtoProfile : Profile
    {
        public DtoProfile()
        {
            CreateMap<Event, Read>()
                .ReverseMap();
            CreateMap<Create, Event>()
                .ForMember(x=>x.DateFrom, opt=>opt.MapFrom(y=>y.Date.Start!.Value.ToUniversalTime()))
                .ForMember(x=>x.DateTo, opt=>opt.MapFrom(y=>y.Date.End!.Value.ToUniversalTime()));
            CreateMap<Event, EventViewModel>();
        }
    }
}
