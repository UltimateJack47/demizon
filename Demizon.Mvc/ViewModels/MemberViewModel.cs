using AutoMapper;
using CryptoHelper;
using Demizon.Dal.Entities;

namespace Demizon.Mvc.ViewModels;

public class MemberViewModel
{
    public int Id { get; set; }

    public string PasswordHash { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Surname { get; set; } = null!;

    public string Login { get; set; } = null!;

    public string? Email { get; set; }

    public string? Password { get; set; }

    public UserRole Role { get; set; }

    public Gender Gender { get; set; }

    public bool IsVisible { get; set; } = false;

    public bool IsAttendanceVisible { get; set; } = true;

    public DateTime? BirthDate { get; set; }

    public DateTime? MemberSince { get; set; }

    public List<FileViewModel> Photos { get; set; } = [];

    public List<AttendanceViewModel> Attendances { get; set; } = [];

    public class DtoProfile : Profile
    {
        public DtoProfile()
        {
            CreateMap<Member, MemberViewModel>()
                .ReverseMap()
                .ForMember(x => x.PasswordHash,
                    opt => opt.MapFrom(y => string.IsNullOrWhiteSpace(y.Password)
                        ? y.PasswordHash
                        : Crypto.HashPassword(y.Password)));
        }
    }
}