using DomProject.Core.Services.Event;
using DomProject.Core.Services.File;
using DomProject.Core.Services.Member;
using DomProject.Core.Services.User;
using DomProject.Core.Services.VideoLink;
using Microsoft.Extensions.DependencyInjection;

namespace DomProject.Core.Extensions;

public static class CoreServicesRegistrationExtension
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.AddTransient<IEventService, EventService>();
        services.AddTransient<IUserService, UserService>();
        services.AddTransient<IFileService, FileService>();
        services.AddTransient<IMemberService, MemberService>();
        services.AddTransient<IVideoLinkService, VideoLinkService>();
        return services;
    }
}

