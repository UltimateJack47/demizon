using AutoMapper;
using DomProject.Dal.Entities;

namespace DomProject.Mvc.DTO;

public class UserDto
{
    public string Name { get; set; } = null!;

    public string Surname { get; set; } = null!;

    public string Login { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public List<BorrowingDto> Borrowings { get; set; } = new();
    
    public class Read : UserDto
    {
        public int Id { get; set; }
    }

    public class Create : UserDto
    {
    }

    public class DtoProfile : Profile
    {
        public DtoProfile()
        {
            CreateMap<User, Read>()
                .ReverseMap();
            CreateMap<Create, User>();
            CreateMap<Read, Create>();
        }
    }
}
