using Demizon.Maui.Services;

namespace Demizon.Maui;

public partial class App : Application
{
    private readonly AppShell _shell;
    private readonly TokenStorage _tokenStorage;
    private readonly NotificationSyncService _notificationSyncService;

    public App(AppShell shell, TokenStorage tokenStorage, NotificationSyncService notificationSyncService, NotificationNavigationService notificationNavigationService)
    {
        InitializeComponent();
        _shell = shell;
        _tokenStorage = tokenStorage;
        _notificationSyncService = notificationSyncService;

        notificationNavigationService.Initialize();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Force light theme — styles are designed for light only; full dark mode is a future task
        UserAppTheme = AppTheme.Light;

        var window = new Window(_shell);

        window.Created += (_, _) =>
        {
            Dispatcher.DispatchAsync(() => TryAutoLoginAsync());
        };

        return window;
    }

    private async Task TryAutoLoginAsync()
    {
        try
        {
            await _tokenStorage.InitializeAsync();

            if (_tokenStorage.IsTokenValid())
            {
                await Shell.Current.GoToAsync(AppRoutes.MainTabs);
                _ = _notificationSyncService.SyncAsync();
                return;
            }

            var refreshToken = await _tokenStorage.GetRefreshTokenAsync();
            if (refreshToken is null) return;

            var login = await _tokenStorage.GetLoginAsync();
            var result = await TokenRefreshHelper.RefreshAsync(refreshToken);

            if (result is not null)
            {
                await _tokenStorage.SaveAsync(result, login ?? string.Empty);
                await Shell.Current.GoToAsync(AppRoutes.MainTabs);
                _ = _notificationSyncService.SyncAsync();
            }
            else
            {
                _tokenStorage.Clear();
            }
        }
        catch
        {
            _tokenStorage.Clear();
        }
    }
}
