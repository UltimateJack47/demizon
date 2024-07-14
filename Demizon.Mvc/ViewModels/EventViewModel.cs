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

    public string? Information { get; set; }

    public DateRange Date { get; set; } = null!;

    public bool IsPublic { get; set; }

    public List<AttendanceViewModel> Attendances { get; set; } = [];

    public class DtoProfile : Profile
    {
        public DtoProfile()
        {
            CreateMap<Event, EventViewModel>()
                .ForMember(x => x.Date,
                    opt => opt.MapFrom(y => new DateRange
                        {Start = y.DateFrom, End = y.DateTo}))
                .ReverseMap()
                .ForMember(x => x.DateFrom, opt => opt.MapFrom(y => y.Date.Start!.Value))
                .ForMember(x => x.DateTo, opt => opt.MapFrom(y => y.Date.End!.Value));
        }
    }
}