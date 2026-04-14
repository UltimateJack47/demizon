namespace Demizon.Maui;

public partial class App : Application
{
    public App(AppShell shell)
    {
        InitializeComponent();
        // MainPage je deprecated v MAUI .NET 10 — inicializace přes CreateWindow
        _shell = shell;
    }

    private readonly AppShell _shell;

    protected override Window CreateWindow(IActivationState? activationState)
        => new Window(_shell);
}
