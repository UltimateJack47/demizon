using AutoMapper;
using DomProject.Dal.Entities;

namespace DomProject.Mvc.DTOs;

public class DeviceDto
{
    public string Name { get; set; } = null!;

    public int Year { get; set; }

    public decimal Price { get; set; }

    public string Description { get; set; } = null!;

    public List<BorrowingDto.Read> Borrowings { get; set; } = new();

    public class Read : DeviceDto
    {
        public int Id { get; set; }
    }
    
    public class Create : DeviceDto
    {
    }
    
    public class DtoProfile : Profile
    {
        public DtoProfile()
        {
            CreateMap<Device, Read>()
                .ReverseMap();
            CreateMap<Create, Device>();
            CreateMap<Read, Create>();
        }
    }
}
