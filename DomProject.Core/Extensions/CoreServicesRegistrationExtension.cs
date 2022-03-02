using DomProject.Core.Services.Borrowing;
using DomProject.Core.Services.Device;
using DomProject.Core.Services.User;
using Microsoft.Extensions.DependencyInjection;

namespace DomProject.Core.Extensions;

public static class CoreServicesRegistrationExtension
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.AddTransient<IDeviceService, DeviceService>();
        services.AddTransient<IUserService, UserService>();
        services.AddTransient<IBorrowingService, BorrowingService>();
        return services;
    }
}

