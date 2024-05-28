using AutoMapper;
using Demizon.Dal.Entities;

namespace Demizon.Mvc.ViewModels;

public class MemberViewModel
{
    public int Id { get; set; }
    
    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public Gender Gender { get; set; }

    public bool IsVisible { get; set; }

    public DateTime? BirthDate { get; set; }

    public DateTime? MemberSince { get; set; }

    public IList<FileViewModel> Photos { get; set; } = new List<FileViewModel>();
    
    public class DtoProfile : Profile
    {
        public DtoProfile()
        {
            CreateMap<Member, MemberViewModel>()
                .ReverseMap();
        }
    }
}
