using AutoMapper;
using DomProject.Dal.Entities;

namespace DomProject.Mvc.ViewModels;

public class MemberViewModel
{
    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public Gender Gender { get; set; }

    public bool IsVisible { get; set; }

    public DateTime? BirthDate { get; set; }

    public DateTime? MemberSince { get; set; }

    public List<FileViewModel> Photos { get; set; } = new List<FileViewModel>();
    
    public class Read : MemberViewModel
    {
        public int Id { get; set; }
    }

    public class Create : MemberViewModel
    {
    }

    public class DtoProfile : Profile
    {
        public DtoProfile()
        {
            CreateMap<Member, Read>()
                .ReverseMap();
            CreateMap<Create, Member>();
            CreateMap<Member, MemberViewModel>();
        }
    }
}
