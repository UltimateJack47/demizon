using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.CookiePolicy;

namespace Demizon.Mvc.Services.Authentication;

public static class MvcAuthenticationServicesRegistrationExtension
{
    /// <summary>
    /// Collection of used services in the Api
    /// </summary>
    /// <param name="services">Collection of used services</param>
    /// <returns>Services that are used in the Api</returns>
    public static IServiceCollection AddAuthenticationServices(this IServiceCollection services)
    {
        services.Configure<CookiePolicyOptions>(options =>
        {
            options.Secure = CookieSecurePolicy.Always;
            options.MinimumSameSitePolicy = SameSiteMode.Lax;
            options.HttpOnly = HttpOnlyPolicy.Always;
        });
        services.AddAuthentication(options => options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
        services.AddScoped<IMyAuthenticationService, MyAuthenticationService>();

        return services;
    }
}
