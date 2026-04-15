using Demizon.Maui.Pages;
using Demizon.Maui.Services;
using Demizon.Maui.ViewModels;
using Microsoft.Extensions.Logging;
using Refit;

namespace Demizon.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        const string baseUrl = ApiConfig.BaseUrl;

        var services = builder.Services;

        // Storage + auth handler
        services.AddSingleton<TokenStorage>();
        services.AddTransient<AuthHandler>();

        // Refit HTTP klient s auth handler
        services.AddRefitClient<IApiClient>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl))
            .AddHttpMessageHandler<AuthHandler>();

        // ViewModels
        services.AddTransient<LoginViewModel>();
        services.AddTransient<EventsViewModel>();
        services.AddTransient<EventDetailViewModel>();
        services.AddTransient<DancesViewModel>();
        services.AddTransient<DanceDetailViewModel>();
        services.AddTransient<ProfileViewModel>();
        services.AddTransient<CreateEventViewModel>();

        // Pages
        services.AddTransient<LoginPage>();
        services.AddTransient<EventsPage>();
        services.AddTransient<EventDetailPage>();
        services.AddTransient<DancesPage>();
        services.AddTransient<DanceDetailPage>();
        services.AddTransient<ProfilePage>();
        services.AddTransient<CreateEventPage>();

        // Shell + App
        services.AddSingleton<AppShell>();
        services.AddSingleton<App>();

        return builder.Build();
    }
}
