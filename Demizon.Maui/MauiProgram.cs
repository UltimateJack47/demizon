using CommunityToolkit.Maui;
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
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit();

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

        // Services
        services.AddTransient<IDocumentService, DocumentService>();

        // ViewModels
        services.AddSingleton<NotificationSyncService>();
        services.AddSingleton<NotificationNavigationService>();
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
        services.AddTransient<MemberAttendanceDetailViewModel>();
        services.AddTransient<EditProfileViewModel>();
        services.AddTransient<ChangePasswordViewModel>();
        services.AddTransient<EditEventViewModel>();
        services.AddTransient<GalleryViewModel>();
        services.AddTransient<PhotoViewerViewModel>();

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
        services.AddTransient<MemberAttendanceDetailPage>();
        services.AddTransient<EditProfilePage>();
        services.AddTransient<ChangePasswordPage>();
        services.AddTransient<EditEventPage>();
        services.AddTransient<GalleryPage>();
        services.AddTransient<PhotoViewerPage>();

        // Shell + App
        services.AddSingleton<AppShell>();
        services.AddSingleton<App>();

        return builder.Build();
    }
}
