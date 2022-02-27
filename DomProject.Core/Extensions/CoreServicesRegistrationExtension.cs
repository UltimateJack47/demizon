using DomProject.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DomProject.Core.Extensions;

public static class CoreServicesRegistrationExtension
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.AddTransient<IDeviceService, DeviceService>();
        return services;
    }
}

