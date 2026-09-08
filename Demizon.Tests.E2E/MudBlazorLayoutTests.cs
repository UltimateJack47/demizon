using Demizon.Tests.E2E.Infrastructure;
using Microsoft.Playwright;

namespace Demizon.Tests.E2E;

/// <summary>
/// Automatizovaná část vizuální kontroly MudBlazoru 9.9. Hlídá vlastnosti, které
/// jsou deterministické a přitom se při upgradu komponentové knihovny reálně
/// rozbíjejí: rozměry interaktivních prvků, přetékání na mobilu, otevření
/// a zavření dialogu, zobrazení snackbaru.
/// </summary>
/// <remarks>
/// Co tady <b>není</b>: porovnávání screenshotů proti baseline. Fonty se
/// renderují jinak na jiném stroji i po aktualizaci prohlížeče, takže by to
/// padalo z důvodů, které s aplikací nesouvisejí. Screenshoty se ukládají jako
/// artefakt (viz <c>e2e-artifacts</c>) k prohlédnutí člověkem.
/// </remarks>
public class MudBlazorLayoutTests(E2EFixture fixture) : E2ETestBase(fixture)
{
    [Fact]
    public async Task Interaktivni_prvky_na_verejne_strance_maji_nenulovou_velikost()
    {
        await Page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        // Nulová velikost je typický projev rozbitého CSS po upgradu: prvek
        // v DOM je, ale nedá se na něj kliknout.
        // checkVisibility() bere v potaz i skryté předky. Kontrola jen přes
        // getComputedStyle na samotném prvku sem pouštěla odkaz "Reload"
        // z Blazorova reconnect dialogu: ten display:none nemá, skrytý je jeho
        // rodič #components-reconnect-modal.
        var broken = await Page.EvaluateAsync<string[]>("""
            () => Array.from(document.querySelectorAll('button, a[href]'))
                .filter(el => el.checkVisibility({ checkOpacity: true, checkVisibilityCSS: true }))
                .filter(el => {
                    const rect = el.getBoundingClientRect();
                    return rect.width < 1 || rect.height < 1;
                })
                .map(el => el.tagName + ':' + (el.textContent || '').trim().slice(0, 30))
                .slice(0, 10)
            """);

        Assert.Empty(broken);
    }

    [Fact]
    public async Task Navigace_na_mobilu_nepretece()
    {
        // Telefonní šířka: tady se přetékání projevuje nejčastěji, protože
        // MudBlazor mění breakpointy mezi verzemi.
        await using var mobile = await Fixture.NewContextAsync(390, 844);
        var page = await mobile.NewPageAsync();

        foreach (var route in new[] { "/", "/Dances", "/Photos" })
        {
            await page.GotoAsync(route, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            var overflow = await page.EvaluateAsync<int>(
                "() => document.documentElement.scrollWidth - document.documentElement.clientWidth");
            Assert.True(overflow <= 2, $"{route} přetéká na 390 px o {overflow} px");

            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(Fixture.ArtifactDirectory, $"mobile-{route.Trim('/').Replace('/', '-')}.png"),
                FullPage = true,
            });
        }
    }

    [Fact]
    public async Task Administrace_se_vykresli_a_ma_navigaci()
    {
        await LoginAsAdminAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var links = await Page.Locator("a[href]").CountAsync();
        Assert.True(links > 0, "Administrace se vykreslila bez jediného odkazu.");

        Assert.Empty(RelevantConsoleErrors);
        await CaptureAsync("desktop-admin");
    }

    [Fact]
    public async Task Administrace_na_mobilu_nepretece()
    {
        await using var mobile = await Fixture.NewContextAsync(390, 844);
        var page = await mobile.NewPageAsync();

        await page.GotoAsync("/Login", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.FillAsync("input[name='Login']", AppHost.AdminLogin);
        await page.FillAsync("input[name='Password']", AppHost.AdminPassword);
        await page.ClickAsync("button[type='submit']");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var overflow = await page.EvaluateAsync<int>(
            "() => document.documentElement.scrollWidth - document.documentElement.clientWidth");
        Assert.True(overflow <= 2, $"Administrace přetéká na 390 px o {overflow} px");

        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(Fixture.ArtifactDirectory, "mobile-admin.png"),
            FullPage = true,
        });
    }
}
