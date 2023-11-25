using AutoMapper;
using CryptoHelper;
using Demizon.Dal.Entities;

namespace Demizon.Mvc.ViewModels;

public class UserViewModel
{
    public int Id { get; set; }

    public string PasswordHash { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Surname { get; set; }

    public string Login { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? Password { get; set; }

    public class DtoProfile : Profile
    {
        public DtoProfile()
        {
            CreateMap<User, UserViewModel>()
                .ReverseMap()
                .ForMember(x => x.PasswordHash,
                    opt => opt.MapFrom(y => Crypto.HashPassword(y.Password)));
        }
    }
}
