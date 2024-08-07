﻿using Demizon.Core.Services.Attendance;
using Demizon.Core.Services.Dance;
using Demizon.Core.Services.Event;
using Demizon.Core.Services.File;
using Demizon.Core.Services.FileUpload;
using Demizon.Core.Services.Member;
using Demizon.Core.Services.VideoLink;
using Microsoft.Extensions.DependencyInjection;

namespace Demizon.Core.Extensions;

public static class CoreServicesRegistrationExtension
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.AddTransient<IEventService, EventService>();
        services.AddTransient<IMemberService, MemberService>();
        services.AddTransient<IFileService, FileService>();
        services.AddTransient<IFileUploadService, FileUploadService>();
        services.AddTransient<IVideoLinkService, VideoLinkService>();
        services.AddTransient<IDanceService, DanceService>();
        services.AddTransient<IAttendanceService, AttendanceService>();
        return services;
    }
}