using Demizon.Maui.Pages;
using Demizon.Maui.Pages.Attendance;
using Demizon.Maui.Services;
using Demizon.Maui.ViewModels;
using Demizon.Maui.ViewModels.Attendance;
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

        // Storage, auth handler, navigation
        services.AddSingleton<TokenStorage>();
        services.AddSingleton<INavigationService, ShellNavigationService>();
        services.AddTransient<AuthHandler>();

        // Refit HTTP klient s auth handler
        services.AddRefitClient<IApiClient>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl))
            .AddHttpMessageHandler<AuthHandler>();

        // ViewModels
        services.AddTransient<LoginViewModel>();
        services.AddTransient<AttendanceViewModel>();
        services.AddTransient<AttendanceStatsViewModel>();
        services.AddTransient<AllMembersAttendanceViewModel>();
        services.AddTransient<EventsViewModel>();
        services.AddTransient<EventDetailViewModel>();
        services.AddTransient<DancesViewModel>();
        services.AddTransient<DanceDetailViewModel>();
        services.AddTransient<ProfileViewModel>();
        services.AddTransient<CreateEventViewModel>();

        // Pages
        services.AddTransient<LoginPage>();
        services.AddTransient<AttendancePage>();
        services.AddTransient<AttendanceStatsPage>();
        services.AddTransient<AllMembersAttendancePage>();
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
