using Demizon.Core.Services.Attendance;
using Demizon.Core.Services.Authentication;
using Demizon.Core.Services.Dance;
using Demizon.Core.Services.Event;
using Demizon.Core.Services.File;
using Demizon.Core.Services.FileUpload;
using Demizon.Core.Services.GoogleCalendar;
using Demizon.Core.Services.Member;
using Demizon.Core.Services.Notification;
using Demizon.Core.Services.Storage;
using Demizon.Core.Services.VideoLink;
using Microsoft.Extensions.DependencyInjection;

namespace Demizon.Core.Extensions;

public static class CoreServicesRegistrationExtension
{
    /// <summary>
    /// Strop pro alokátor ImageSharpu. Pojistka pro případ, že by k nám dorazil obrázek,
    /// na který nestačí ani škálovaný dekód ve <see cref="Services.FileUpload.FileUploadService"/> —
    /// místo vyčerpání paměti kontejneru skončí zpracování výjimkou.
    /// </summary>
    private const int ImageAllocationLimitMegabytes = 128;

    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        SixLabors.ImageSharp.Configuration.Default.MemoryAllocator =
            SixLabors.ImageSharp.Memory.MemoryAllocator.Create(
                new SixLabors.ImageSharp.Memory.MemoryAllocatorOptions
                {
                    AllocationLimitMegabytes = ImageAllocationLimitMegabytes
                });

        services.AddTransient<IEventService, EventService>();
        services.AddTransient<IMemberService, MemberService>();
        services.AddTransient<IFileService, FileService>();
        services.AddTransient<IFileUploadService, FileUploadService>();
        services.AddScoped<IStorageQuotaService, StorageQuotaService>();
        services.AddTransient<IVideoLinkService, VideoLinkService>();
        services.AddTransient<IDanceService, DanceService>();
        services.AddTransient<IAttendanceService, AttendanceService>();
        services.AddTransient<IPushSubscriptionService, PushSubscriptionService>();
        services.AddTransient<IAttendanceReportService, AttendanceReportService>();
        services.AddTransient<IGoogleCalendarService, GoogleCalendarService>();
        services.AddSingleton<TokenService>();
        services.AddScoped<RefreshTokenService>();
        return services;
    }
}
