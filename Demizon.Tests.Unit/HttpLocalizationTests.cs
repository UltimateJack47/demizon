using System.Net;
using System.Net.Http.Headers;
using Demizon.Tests.Unit.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

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

    // --- /SetLanguage: ruční volba jazyka z layoutu ---

    private async Task<HttpResponseMessage> SetLanguageAsync(string path, string? referer = null)
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (referer is not null)
            request.Headers.Referrer = new Uri(referer);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task SetLanguage_ulozi_cookie_a_vrati_na_puvodni_stranku()
    {
        var response = await SetLanguageAsync("/SetLanguage/cs?returnUrl=%2FEvents%3Fmonth%3D10");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Events?month=10", response.Headers.Location?.OriginalString);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), c => c.Contains("cs-CZ"));
    }

    /// <summary>Dřív <c>Redirect("")</c> → HTTP 500, když prohlížeč Referer neposlal.</summary>
    [Fact]
    public async Task SetLanguage_bez_returnUrl_presmeruje_na_titulni_stranku()
    {
        var response = await SetLanguageAsync("/SetLanguage/en");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    /// <summary>
    /// Open redirect: odkaz <c>demizon.cz/SetLanguage/cs?returnUrl=…</c> nesmí
    /// poslat návštěvníka na cizí web. <c>//</c> a <c>/\</c> čte prohlížeč
    /// jako adresu jiného serveru. Cizí Referer se ignoruje úplně.
    /// </summary>
    [Theory]
    [InlineData("https%3A%2F%2Fevil.example%2F", null)]
    [InlineData("%2F%2Fevil.example", null)]
    [InlineData("%2F%5Cevil.example", null)]
    [InlineData(null, "https://evil.example/phish")]
    public async Task SetLanguage_nepusti_na_cizi_web(string? returnUrl, string? referer)
    {
        var path = "/SetLanguage/cs" + (returnUrl is null ? "" : $"?returnUrl={returnUrl}");

        var response = await SetLanguageAsync(path, referer);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }
}
