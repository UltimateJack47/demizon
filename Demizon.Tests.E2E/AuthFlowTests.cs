using Demizon.Tests.E2E.Infrastructure;
using Microsoft.Playwright;

namespace Demizon.Tests.E2E;

/// <summary>
/// Přihlášení a přístup do administrace přes skutečný prohlížeč.
/// </summary>
/// <remarks>
/// <c>AuthApiTests</c> testují JWT pro mobil; tady jde o **cookie** cestu, kterou
/// používá web. Ta se přes <c>HttpClient</c> testuje špatně: `/ProcessLogin` je
/// form post s redirectem, cookie je `HttpOnly` a `Secure` politika se liší podle
/// prostředí. Prohlížeč to udělá tak, jak to udělá uživatel.
/// </remarks>
public class AuthFlowTests(E2EFixture fixture) : E2ETestBase(fixture)
{
    [Fact]
    public async Task Spravne_heslo_pusti_do_administrace()
    {
        await LoginAsAdminAsync();

        Assert.Contains("/Admin", Page.Url);
        Assert.Empty(RelevantConsoleErrors);
    }

    [Fact]
    public async Task Spatne_heslo_vrati_na_login_s_chybou()
    {
        await LoginAsync(AppHost.AdminLogin, "tohle-heslo-neni-spravne");

        // Neúspěch se pozná podle /Login/true — ta stránka vypisuje InvalidSignIn.
        Assert.Contains("/Login", Page.Url);
        Assert.DoesNotContain("/Admin", Page.Url);
    }

    [Fact]
    public async Task Neznamy_login_neprojde()
    {
        await LoginAsync("nikdo-takovy-neexistuje", "cokoli-dost-dlouhe");

        Assert.Contains("/Login", Page.Url);
        Assert.DoesNotContain("/Admin", Page.Url);
    }

    [Fact]
    public async Task Neprihlaseny_se_do_administrace_nedostane()
    {
        await Page.GotoAsync("/Admin", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        // Blazor stránku vyrenderuje, ale AuthorizeView admin obsah nepustí —
        // ověřuje se tedy nepřítomnost obsahu, ne redirect.
        var body = await Page.InnerTextAsync("body");
        Assert.DoesNotContain("Docházka", body);
    }

    [Fact]
    public async Task Odhlaseni_zavre_pristup_do_administrace()
    {
        await LoginAsAdminAsync();
        Assert.Contains("/Admin", Page.Url);

        await Page.GotoAsync("/Logout", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        await Page.GotoAsync("/Admin", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        var body = await Page.InnerTextAsync("body");
        Assert.DoesNotContain("Docházka", body);
    }

    [Fact]
    public async Task Bezny_clen_se_prihlasi_taky()
    {
        await LoginAsMemberAsync();

        Assert.Contains("/Admin", Page.Url);
        Assert.Empty(RelevantConsoleErrors);
    }
}
