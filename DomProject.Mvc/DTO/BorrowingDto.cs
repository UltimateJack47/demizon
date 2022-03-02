using AutoMapper;
using DomProject.Dal.Entities;

namespace DomProject.Mvc.DTO;

public class BorrowingDto
{
    public int DeviceId { get; set; }

    public DeviceDto.Read Device { get; set; } = null!;

    public int UserId { get; set; }

    public UserDto.Read User { get; set; } = null!;

    public DateTime Start { get; set; }

    public DateTime? End { get; set; }
    

    public class Read : BorrowingDto
    {
        public int Id { get; set; }
    }

    public class Create : BorrowingDto
    {
    }

    public class DtoProfile : Profile
    {
        public DtoProfile()
        {
            CreateMap<Borrowing, Read>()
                .ReverseMap();
            CreateMap<Create, Borrowing>();
            CreateMap<Read, Create>();
        }
    }
}
