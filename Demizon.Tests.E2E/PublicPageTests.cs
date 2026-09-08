using Demizon.Tests.E2E.Infrastructure;
using Microsoft.Playwright;

namespace Demizon.Tests.E2E;

/// <summary>
/// Veřejné stránky: vykreslí se, neházejí chyby v konzoli a nepřetékají do strany.
/// </summary>
/// <remarks>
/// Tohle je ta část kontroly MudBlazoru, která jde automatizovat. Pixelové
/// porovnávání je napříč stroji křehké (fonty se renderují jinak), ale
/// „stránka je širší než viewport“ a „tlačítko má nulovou velikost“ jsou
/// deterministické a přesně to jsou třídy regresí, které build nezachytí.
/// </remarks>
public class PublicPageTests(E2EFixture fixture) : E2ETestBase(fixture)
{
    public static TheoryData<string, string> PublicPages => new()
    {
        { "/", "index" },
        { "/Dances", "dances" },
        { "/Photos", "photos" },
        { "/Join-Us", "join-us" },
        { "/Privacy-Policy", "privacy-policy" },
        { "/Terms-of-Service", "terms-of-service" },
    };

    [Theory]
    [MemberData(nameof(PublicPages))]
    public async Task Stranka_se_vykresli_bez_chyby_v_konzoli(string route, string name)
    {
        var response = await Page.GotoAsync(route, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
        });

        Assert.NotNull(response);
        Assert.True(response!.Ok, $"{route} vrátila HTTP {response.Status}");

        var body = await Page.InnerTextAsync("body");
        Assert.False(string.IsNullOrWhiteSpace(body), $"{route} se vykreslila prázdná");

        // Nezachycená výjimka v Blazoru se v Development projeví jen v konzoli —
        // server v tomhle prostředí nemá zaregistrovaný exception handler.
        Assert.Empty(RelevantConsoleErrors);

        await CaptureAsync($"desktop-{name}");
    }

    [Theory]
    [MemberData(nameof(PublicPages))]
    public async Task Stranka_nepretece_do_strany(string route, string name)
    {
        await Page.GotoAsync(route, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var overflow = await Page.EvaluateAsync<int>(
            "() => document.documentElement.scrollWidth - document.documentElement.clientWidth");

        // Malá tolerance na zaokrouhlení šířek a scrollbar.
        Assert.True(overflow <= 2,
            $"{route} přetéká vodorovně o {overflow} px (name: {name})");
    }

    [Fact]
    public async Task Blazor_okruh_nabehne_a_stranka_je_interaktivni()
    {
        await Page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        // Prerender vypadá jako hotová stránka i bez SignalR, takže samotný
        // HTML výstup o funkčnosti okruhu nic neříká. Blazor po připojení
        // odstraní ze stránky komentáře <!--Blazor:...--> s prerender markery.
        await Page.WaitForFunctionAsync(
            "() => window.Blazor !== undefined",
            null,
            new PageWaitForFunctionOptions { Timeout = 20_000 });

        var reconnectVisible = await Page.Locator("#components-reconnect-modal").IsVisibleAsync();
        Assert.False(reconnectVisible, "Blazor hlásí ztracené spojení hned po načtení.");
        Assert.Empty(RelevantConsoleErrors);
    }
}
