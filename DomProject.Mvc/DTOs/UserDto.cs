using AutoMapper;
using CryptoHelper;
using DomProject.Dal.Entities;

namespace DomProject.Mvc.DTOs;

public class UserDto
{
    public string Name { get; set; } = null!;

    public string? Surname { get; set; }

    public string Login { get; set; } = null!;

    public string Email { get; set; } = null!;

    public List<BorrowingDto.Read> Borrowings { get; set; } = new();

    public class Read : UserDto
    {
        public int Id { get; set; }

        public string PasswordHash { get; set; } = null!;
    }

    public class Create : UserDto
    {
        public string Password { get; set; } = null!;
    }

    public class DtoProfile : Profile
    {
        public DtoProfile()
        {
            CreateMap<User, Read>()
                .ReverseMap();
            CreateMap<Create, User>()
                .ForMember(x => x.PasswordHash,
                    opt => opt.MapFrom(y => Crypto.HashPassword(y.Password)));
            CreateMap<Read, Create>();
        }
    }
}
