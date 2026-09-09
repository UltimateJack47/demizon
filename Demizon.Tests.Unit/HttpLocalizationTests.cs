using System.Net.Http.Headers;
using Demizon.Tests.Unit.Infrastructure;

namespace Demizon.Tests.Unit;

/// <summary>
/// Vyjednávání jazyka podle <c>Accept-Language</c>. Veřejné stránky jsou
/// dvojjazyčné (cs/en) a jazyk se určuje z prohlížeče, takže je to kontrakt,
/// který nesmí tiše spadnout na výchozí angličtinu.
/// </summary>
/// <remarks>
/// Testuje se na HTTP úrovni, ne v E2E. Kulturu Blazor <b>okruhu</b> určuje
/// request, kterým se okruh naváže (WebSocket handshake na <c>/_blazor</c>),
/// a na ten se hlavičky nastavené Playwrightem nevztahují — obsah dorenderovaný
/// okruhem by tam tedy vyšel anglicky bez ohledu na aplikaci. Skutečný
/// prohlížeč <c>Accept-Language</c> na handshake posílá, takže uživatel vidí
/// česky i po připojení okruhu (ověřeno ručně v Chromu).
/// <para>
/// Pořadí providerů v <c>Program.cs</c>: cookie (ruční volba) → remap
/// slovenštiny na češtinu → <c>Accept-Language</c>. Výchozí je <c>en-US</c>.
/// </para>
/// </remarks>
[Collection("WebHost")]
public class HttpLocalizationTests(AuthApiFactory factory)
{
    private async Task<string> LoginPageAsync(string? acceptLanguage)
    {
        using var client = factory.CreateClient();
        if (acceptLanguage is not null)
        {
            client.DefaultRequestHeaders.AcceptLanguage.Add(
                new StringWithQualityHeaderValue(acceptLanguage));
        }

        return await client.GetStringAsync("/Login");
    }

    [Theory]
    [InlineData("cs-CZ")]
    [InlineData("cs")]
    public async Task Cesky_prohlizec_dostane_ceskou_verzi(string acceptLanguage)
    {
        var page = await LoginPageAsync(acceptLanguage);

        Assert.Contains("Zapamatovat", page);
        Assert.DoesNotContain("Remember me", page);
    }

    /// <summary>
    /// Slovenština se záměrně mapuje na češtinu — vlastní provider
    /// v <c>Program.cs</c>. Slovák by jinak spadl na angličtinu.
    /// </summary>
    [Fact]
    public async Task Slovensky_prohlizec_dostane_taky_ceskou_verzi()
    {
        var page = await LoginPageAsync("sk-SK");

        Assert.Contains("Zapamatovat", page);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    [InlineData(null)]
    public async Task Ostatni_dostanou_anglickou_verzi(string? acceptLanguage)
    {
        var page = await LoginPageAsync(acceptLanguage);

        Assert.Contains("Remember me", page);
        Assert.DoesNotContain("Zapamatovat", page);
    }
}
