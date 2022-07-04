using AutoMapper;
using File = Demizon.Dal.Entities.File;

namespace Demizon.Mvc.ViewModels;

public class FileViewModel
{
    public string Path { get; set; } = null!;

    public MemberViewModel? Member { get; set; }

    public int? MemberId { get; set; }

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
            CreateMap<File, Read>()
                .ReverseMap();
            CreateMap<Create, File>();
            CreateMap<File, MemberViewModel>();
        }
    }
}
