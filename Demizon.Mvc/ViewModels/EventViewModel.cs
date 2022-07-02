using AutoMapper;
using DomProject.Dal.Entities;

namespace DomProject.Mvc.ViewModels;

public class EventViewModel
{
    public string Name { get; set; } = null!;

    public DateTime Date { get; set; }

    public string? Place { get; set; }
    

    public class Read : EventViewModel
    {
        public int Id { get; set; }
    }

    public class Create : EventViewModel
    {
    }

    public class DtoProfile : Profile
    {
        public DtoProfile()
        {
            CreateMap<Event, Read>()
                .ReverseMap();
            CreateMap<Create, Event>();
            CreateMap<Event, EventViewModel>();
        }
    }
}
