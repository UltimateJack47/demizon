﻿using AutoMapper;
using Demizon.Dal.Entities;

namespace Demizon.Mvc.ViewModels;

public class AttendanceViewModel
{
    public int Id { get; set; }

    public bool Attends { get; set; } = false;

    public string? Comment { get; set; }

    public DateTime Date { get; set; }

    public int? EventId { get; set; }

    public EventViewModel? Event { get; set; }

    public int MemberId { get; set; }

    public MemberViewModel Member { get; set; } = null!;

    public class DtoProfile : Profile
    {
        public DtoProfile()
        {
            CreateMap<Attendance, AttendanceViewModel>()
                .ReverseMap();
        }
    }
}
