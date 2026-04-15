using Demizon.Maui.Services;

namespace Demizon.Maui;

public partial class App : Application
{
    private readonly AppShell _shell;
    private readonly TokenStorage _tokenStorage;

    public App(AppShell shell, TokenStorage tokenStorage)
    {
        InitializeComponent();
        _shell = shell;
        _tokenStorage = tokenStorage;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Force light theme to match MVC app appearance
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
