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

    public bool IsDancer { get; set; } = false;

    public bool IsMusician { get; set; } = false;

    public bool IsExternal { get; set; } = false;

    public DateTime? BirthDate { get; set; }

    public DateTime? MemberSince { get; set; }

    public List<FileViewModel> Photos { get; set; } = [];

    public List<AttendanceViewModel> Attendances { get; set; } = [];

    public string? GoogleRefreshToken { get; set; }

    public string? GoogleCalendarId { get; set; }

    public DateTime? GoogleConnectedAt { get; set; }
}

public static class MemberMappingExtensions
{
    public static MemberViewModel ToViewModel(this Member entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Surname = entity.Surname,
        Login = entity.Login,
        Email = entity.Email,
        PasswordHash = entity.PasswordHash,
        Role = entity.Role,
        Gender = entity.Gender,
        IsVisible = entity.IsVisible,
        IsAttendanceVisible = entity.IsAttendanceVisible,
        IsDancer = entity.IsDancer,
        IsMusician = entity.IsMusician,
        IsExternal = entity.IsExternal,
        BirthDate = entity.BirthDate,
        MemberSince = entity.MemberSince,
        GoogleRefreshToken = entity.GoogleRefreshToken,
        GoogleCalendarId = entity.GoogleCalendarId,
        GoogleConnectedAt = entity.GoogleConnectedAt,
    };

    public static Member ToEntity(this MemberViewModel vm) => new()
    {
        Id = vm.Id,
        Name = vm.Name,
        Surname = vm.Surname,
        Login = vm.Login,
        Email = vm.Email,
        PasswordHash = string.IsNullOrWhiteSpace(vm.Password)
            ? vm.PasswordHash
            : Crypto.HashPassword(vm.Password),
        Role = vm.Role,
        Gender = vm.Gender,
        IsVisible = !vm.IsExternal && vm.IsVisible,
        IsAttendanceVisible = vm.IsAttendanceVisible,
        IsDancer = vm.IsDancer,
        IsMusician = vm.IsMusician,
        IsExternal = vm.IsExternal,
        BirthDate = vm.BirthDate,
        MemberSince = vm.MemberSince,
        GoogleRefreshToken = vm.GoogleRefreshToken,
        GoogleCalendarId = vm.GoogleCalendarId,
        GoogleConnectedAt = vm.GoogleConnectedAt,
    };
}
