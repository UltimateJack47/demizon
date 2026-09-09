using Microsoft.Playwright;

namespace Demizon.Tests.E2E.Infrastructure;

/// <summary>
/// Jeden běžící host a jeden prohlížeč na celou sadu. Start hosta i prohlížeče
/// stojí sekundy, takže se nesmí dělat na každou testovací třídu.
/// </summary>
public sealed class E2EFixture : IAsyncLifetime
{
    public AppHost App { get; } = new();

    private IPlaywright _playwright = null!;
    public IBrowser Browser { get; private set; } = null!;

    /// <summary>Kam se ukládají screenshoty (artefakt pro vizuální kontrolu).</summary>
    public string ArtifactDirectory { get; } = Path.Combine(
        new DirectoryInfo(AppContext.BaseDirectory).FullName, "e2e-artifacts");

    public async Task InitializeAsync()
    {
        await App.StartAsync();

        _playwright = await Playwright.CreateAsync();
        try
        {
            Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
            });
        }
        catch (PlaywrightException ex)
        {
            throw new InvalidOperationException(
                "Chromium pro Playwright není nainstalovaný. Spusť jednou:" + Environment.NewLine +
                "  pwsh Demizon.Tests.E2E/bin/Release/net10.0/playwright.ps1 install chromium" +
                Environment.NewLine + "(v CI to dělá krok 'Install Chromium'.)", ex);
        }

        Directory.CreateDirectory(ArtifactDirectory);
    }

    /// <summary>
    /// Čerstvý kontext na každý test: vlastní cookies a storage, takže se testy
    /// navzájem nepřihlašují ani neodhlašují.
    /// </summary>
    public Task<IBrowserContext> NewContextAsync(int width = 1440, int height = 900) =>
        Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = width, Height = height },
            BaseURL = App.BaseUrl,
            Locale = "cs-CZ",
            // Locale sám nastaví navigator.language, ale hlavička se přidává
            // ručně, aby prerenderované stránky přišly česky jako u skutečného
            // prohlížeče.
            //
            // POZOR: na obsah dorenderovaný Blazor okruhem to nemá vliv. Kulturu
            // okruhu určuje request, kterým se okruh naváže — WebSocket handshake
            // na /_blazor — a na ten se ExtraHTTPHeaders nevztahuje. Proto se
            // v téhle sadě nedá spolehlivě testovat lokalizace obsahu tabulek;
            // vyjednávání kultury hlídá HttpLocalizationTests na HTTP úrovni.
            // (Skutečný prohlížeč Accept-Language na handshake posílá, takže
            // uživatel vidí česky i po připojení okruhu — ověřeno v Chromu.)
            ExtraHTTPHeaders = new Dictionary<string, string>
            {
                ["Accept-Language"] = "cs-CZ,cs;q=0.9,en;q=0.8",
            },
        });

    public async Task DisposeAsync()
    {
        if (Browser is not null) await Browser.CloseAsync();
        _playwright?.Dispose();
        await App.DisposeAsync();
    }
}

[CollectionDefinition("E2E")]
public sealed class E2ECollection : ICollectionFixture<E2EFixture>;
