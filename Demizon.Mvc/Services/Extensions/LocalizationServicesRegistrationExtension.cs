using System.Globalization;
using Microsoft.AspNetCore.Localization;

namespace Demizon.Mvc.Services.Extensions;

public static class LocalizationServicesRegistrationExtension
{
    private const string enUSCulture = "en-US";
    private const string csCZCulture = "cs-CZ";

    /// <summary>
    /// Collection of used services in the Api
    /// </summary>
    /// <param name="services">Collection of used services</param>
    /// <returns>Services that are used in the Api</returns>
    public static IServiceCollection AddAppLocalizationServices(this IServiceCollection services)
    {
        services.AddLocalization(options => options.ResourcesPath = "Resources");
        services.Configure<RequestLocalizationOptions>(options =>
        {
            var supportedCultures = new[]
            {
                new CultureInfo(enUSCulture),
                new CultureInfo(csCZCulture)
            };
            options.DefaultRequestCulture = new RequestCulture(culture: enUSCulture, uiCulture: enUSCulture);
            options.SupportedCultures = supportedCultures;
            options.SupportedUICultures = supportedCultures;

            options.AddInitialRequestCultureProvider(new CustomRequestCultureProvider(async context => await Task.FromResult(new ProviderCultureResult("en"))));
        });
        return services;
    }
}
