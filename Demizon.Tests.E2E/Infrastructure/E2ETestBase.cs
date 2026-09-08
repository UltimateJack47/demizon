using Microsoft.Playwright;

namespace Demizon.Tests.E2E.Infrastructure;

/// <summary>
/// Sdílená obsluha: vlastní browser context na test, sběr chyb z konzole
/// a přihlášení. Chyby z konzole se sbírají u každého testu, protože v Blazoru
/// se rozbitý JavaScript neprojeví výjimkou na serveru — stránka jen přestane
/// reagovat a test by jinak spadl na nesouvisejícím timeoutu.
/// </summary>
[Collection("E2E")]
public abstract class E2ETestBase : IAsyncLifetime
{
    protected E2EFixture Fixture { get; }
    protected IBrowserContext Context { get; private set; } = null!;
    protected IPage Page { get; private set; } = null!;

    private readonly List<string> _consoleErrors = [];

    protected E2ETestBase(E2EFixture fixture) => Fixture = fixture;

    /// <summary>Šířka viewportu; mobilní testy ji přepisují.</summary>
    protected virtual int ViewportWidth => 1440;

    protected virtual int ViewportHeight => 900;

    public virtual async Task InitializeAsync()
    {
        Context = await Fixture.NewContextAsync(ViewportWidth, ViewportHeight);
        Page = await Context.NewPageAsync();

        Page.Console += (_, msg) =>
        {
            if (msg.Type == "error") lock (_consoleErrors) _consoleErrors.Add(msg.Text);
        };
        Page.PageError += (_, error) =>
        {
            lock (_consoleErrors) _consoleErrors.Add(error);
        };
    }

    public virtual async Task DisposeAsync()
    {
        await Context.CloseAsync();
    }

    protected IReadOnlyList<string> ConsoleErrors
    {
        get { lock (_consoleErrors) return _consoleErrors.ToList(); }
    }

    /// <summary>
    /// Chyby, které nevypovídají o aplikaci: chybějící favicon, zablokované
    /// externí zdroje. Filtrují se, aby test nepadal na šumu.
    /// </summary>
    protected IReadOnlyList<string> RelevantConsoleErrors =>
        ConsoleErrors
            .Where(e => !e.Contains("favicon", StringComparison.OrdinalIgnoreCase))
            .Where(e => !e.Contains("net::ERR_INTERNET_DISCONNECTED", StringComparison.OrdinalIgnoreCase))
            .ToList();

    protected async Task LoginAsync(string login, string password)
    {
        // Jména polí jsou Login/Password (velkým) — form je čte přes
        // context.Request.Form["Login"], takže se nesmí přepsat.
        await Page.GotoAsync("/Login", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Page.FillAsync("input[name='Login']", login);
        await Page.FillAsync("input[name='Password']", password);
        await Page.ClickAsync("button[type='submit']");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    protected Task LoginAsAdminAsync() => LoginAsync(AppHost.AdminLogin, AppHost.AdminPassword);

    protected Task LoginAsMemberAsync() => LoginAsync(AppHost.MemberLogin, AppHost.MemberPassword);

    /// <summary>
    /// Screenshot jako artefakt, ne jako assertion. Pixelové porovnávání je
    /// napříč stroji křehké (fonty), takže se tady jen ukládá k prohlédnutí
    /// a k porovnání při příštím upgradu MudBlazoru.
    /// </summary>
    protected async Task CaptureAsync(string name)
    {
        var path = Path.Combine(Fixture.ArtifactDirectory, $"{name}.png");
        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = path, FullPage = true });
    }
}
