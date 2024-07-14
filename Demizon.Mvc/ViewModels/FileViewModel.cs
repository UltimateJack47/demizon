using AutoMapper;
using File = Demizon.Dal.Entities.File;

namespace Demizon.Mvc.ViewModels;

public class FileViewModel
{
    public int Id { get; set; }


    public MemberViewModel? Member { get; set; }

    public int? MemberId { get; set; }

    public string Path { get; set; } = null!;

    public string FileExtension { get; set; } = null!;

    public string ContentType { get; set; } = null!;

    public long FileSize { get; set; }

    public int? DanceId { get; set; }

    public DanceViewModel? Dance { get; set; }

    public class DtoProfile : Profile
    {
        public DtoProfile()
        {
            CreateMap<File, FileViewModel>()
                .ReverseMap();
        }
    }
}