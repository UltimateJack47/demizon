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
    /// Vodorovný přesah stránky v px a k tomu prvky, které za něj mohou.
    /// Bez pojmenování viníka je „přetéká o 9 px“ nezjistitelné — u tabulky
    /// s desítkami buněk se ručně hledá dlouho.
    /// </summary>
    protected static async Task<(int Overflow, string[] Culprits)> MeasureOverflowAsync(IPage page)
    {
        var overflow = await page.EvaluateAsync<int>(
            "() => document.documentElement.scrollWidth - document.documentElement.clientWidth");

        if (overflow <= 2) return (overflow, []);

        var culprits = await page.EvaluateAsync<string[]>("""
            () => {
                const limit = document.documentElement.clientWidth;
                return Array.from(document.querySelectorAll('*'))
                    .filter(el => el.checkVisibility({ checkVisibilityCSS: true }))
                    .map(el => ({ el, rect: el.getBoundingClientRect() }))
                    .filter(x => x.rect.right > limit + 2 && x.rect.width > 0)
                    .sort((a, b) => b.rect.right - a.rect.right)
                    .slice(0, 6)
                    .map(x => {
                        const cls = (x.el.className || '').toString().trim().split(/\s+/).slice(0, 3).join('.');
                        return `${x.el.tagName}${cls ? '.' + cls : ''} right=${Math.round(x.rect.right)} w=${Math.round(x.rect.width)}`;
                    });
            }
            """);

        return (overflow, culprits);
    }

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
