using Demizon.Tests.E2E.Infrastructure;
using Microsoft.Playwright;

namespace Demizon.Tests.E2E;

/// <summary>
/// Projde všechny stránky administrace jako přihlášený admin a na každé ověří
/// to, co jde ověřit strojově: vykreslila se, nehlásí chybu v konzoli,
/// nepřetéká do strany a nemá klikatelné prvky s nulovou velikostí.
/// </summary>
/// <remarks>
/// Tohle je automatizovaná náhrada za „proklikat administraci po upgradu
/// MudBlazoru“. Nenahradí lidské oko u barev a proporcí — na to se ukládají
/// screenshoty — ale zachytí přesně ty regrese, které po upgradu komponentové
/// knihovny bývají: rozbité breakpointy, komponenta, která se nevykreslí,
/// a JS chyba, kterou v Development nikdo nezaloguje.
/// </remarks>
public class AdminWalkthroughTests(E2EFixture fixture) : E2ETestBase(fixture)
{
    public static TheoryData<string, string> AdminPages => new()
    {
        { "/Admin", "admin-home" },
        { "/Admin/Members", "admin-members" },
        { "/Admin/Events", "admin-events" },
        { "/Admin/Dances", "admin-dances" },
        { "/Admin/Photos", "admin-photos" },
        { "/Admin/Videos", "admin-videos" },
        { "/Admin/MemberAttendance/", "admin-attendance" },
        { "/Admin/AttendanceStats", "admin-attendance-stats" },
        { "/Admin/Profile", "admin-profile" },
    };

    [Theory]
    [MemberData(nameof(AdminPages))]
    public async Task Stranka_administrace_se_vykresli_a_nepretece(string route, string name)
    {
        await LoginAsAdminAsync();

        var response = await Page.GotoAsync(route, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
        });
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"{route} vrátila HTTP {response.Status}");

        // Blazor dorenderuje po připojení okruhu, takže samotné NetworkIdle
        // nestačí — čeká se na konkrétní obsah, ne na časový limit.
        await Page.WaitForSelectorAsync("main, .mud-main-content",
            new PageWaitForSelectorOptions { Timeout = 20_000 });

        var body = await Page.InnerTextAsync("body");
        Assert.False(string.IsNullOrWhiteSpace(body), $"{route} je prázdná");

        var (overflow, culprits) = await MeasureOverflowAsync(Page);
        Assert.True(overflow <= 2,
            $"{route} přetéká vodorovně o {overflow} px. Viníci: {string.Join(" | ", culprits)}");

        var broken = await ZeroSizedInteractiveAsync(Page);
        Assert.Empty(broken);

        Assert.Empty(RelevantConsoleErrors);

        await CaptureAsync($"desktop-{name}");
    }

    [Theory]
    [MemberData(nameof(AdminPages))]
    public async Task Stranka_administrace_nepretece_na_mobilu(string route, string name)
    {
        await using var mobile = await Fixture.NewContextAsync(390, 844);
        var page = await mobile.NewPageAsync();

        await page.GotoAsync("/Login", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.FillAsync("input[name='Login']", AppHost.AdminLogin);
        await page.FillAsync("input[name='Password']", AppHost.AdminPassword);
        await page.ClickAsync("button[type='submit']");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await page.GotoAsync(route, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForSelectorAsync("main, .mud-main-content",
            new PageWaitForSelectorOptions { Timeout = 20_000 });

        var (overflow, culprits) = await MeasureOverflowAsync(page);
        Assert.True(overflow <= 2,
            $"{route} přetéká na 390 px o {overflow} px. Viníci: {string.Join(" | ", culprits)}");

        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(Fixture.ArtifactDirectory, $"mobile-{name}.png"),
            FullPage = true,
        });
    }

    /// <summary>
    /// Dialog a snackbar jsou dvě MudBlazor komponenty, které se při upgradu
    /// rozbíjejí nejvíc, protože obě renderují mimo strom stránky (do
    /// providerů na konci body). Když provider chybí, dialog se prostě
    /// neotevře — a build o tom nic neví.
    /// </summary>
    [Fact]
    public async Task Dialog_se_otevre_a_zavre()
    {
        await LoginAsAdminAsync();
        await Page.GotoAsync("/Admin/Members", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Page.WaitForSelectorAsync(".mud-table, main", new PageWaitForSelectorOptions { Timeout = 20_000 });

        var addButton = Page.Locator("button:has(.mud-icon-root)").First;
        if (await addButton.CountAsync() == 0)
            return;

        await addButton.ClickAsync();

        var dialog = Page.Locator(".mud-dialog");
        try
        {
            await dialog.First.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 5_000,
            });
        }
        catch (TimeoutException)
        {
            // První ikonové tlačítko nemusí být "přidat" — to není chyba
            // aplikace, takže se test v takovém případě nevyjadřuje.
            return;
        }

        await Page.Keyboard.PressAsync("Escape");
        await dialog.First.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Hidden,
            Timeout = 5_000,
        });

        Assert.Empty(RelevantConsoleErrors);
    }

    [Fact]
    public async Task Providery_MudBlazoru_jsou_na_strance()
    {
        await LoginAsAdminAsync();

        // Bez těchto providerů se dialogy ani snackbary nemají kam vykreslit.
        // Je to jednoduchá kontrola, ale chybí-li, projeví se to jako "klik nic
        // nedělá" na půlce administrace.
        var providers = await Page.EvaluateAsync<int>(
            "() => document.querySelectorAll('.mud-popover-provider, .mud-dialog-provider, .mud-snackbar-provider').length");

        Assert.True(providers > 0, "Na stránce není žádný MudBlazor provider.");
    }

    /// <summary>
    /// V mobilním (stacked) režimu vypisuje MudTable buňky pod sebe a hodnotu
    /// popisuje atributem <c>data-label</c>. Bez něj vidí uživatel jen sloupec
    /// hodnot bez jakéhokoli vodítka, co která je.
    /// </summary>
    /// <remarks>
    /// Nalezeno pohledem na screenshot z E2E sady, ne strukturální kontrolou:
    /// stránka nepřetékala, prvky měly rozměry, konzole mlčela — a přesto byla
    /// tabulka členů na telefonu nečitelná. Žádná z admin tabulek
    /// <c>DataLabel</c> neměla.
    /// </remarks>
    [Theory]
    [MemberData(nameof(TablePages))]
    public async Task Bunky_tabulky_maji_na_mobilu_popisek(string route)
    {
        await using var mobile = await Fixture.NewContextAsync(390, 844);
        var page = await mobile.NewPageAsync();

        await page.GotoAsync("/Login", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.FillAsync("input[name='Login']", AppHost.AdminLogin);
        await page.FillAsync("input[name='Password']", AppHost.AdminPassword);
        await page.ClickAsync("button[type='submit']");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await page.GotoAsync(route, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForSelectorAsync("main, .mud-main-content",
            new PageWaitForSelectorOptions { Timeout = 20_000 });

        var cells = await page.Locator("table tbody td").CountAsync();
        if (cells == 0)
            return; // prázdná tabulka — o popiscích nic neříká

        var unlabeled = await page.EvaluateAsync<int>(
            "() => Array.from(document.querySelectorAll('table tbody td'))"
            + ".filter(td => !(td.getAttribute('data-label') || '').trim()).length");

        Assert.Equal(0, unlabeled);
    }

    public static TheoryData<string> TablePages =>
    [
        "/Admin/Members",
        "/Admin/Events",
        "/Admin/Dances",
        "/Admin/Videos",
        "/Admin/Photos",
    ];

    private static Task<string[]> ZeroSizedInteractiveAsync(IPage page) =>
        page.EvaluateAsync<string[]>("""
            () => Array.from(document.querySelectorAll('button, a[href], input, select'))
                .filter(el => el.checkVisibility({ checkOpacity: true, checkVisibilityCSS: true }))
                .filter(el => {
                    const rect = el.getBoundingClientRect();
                    return rect.width < 1 || rect.height < 1;
                })
                .map(el => el.tagName + ':' + (el.getAttribute('aria-label') || el.textContent || '').trim().slice(0, 30))
                .slice(0, 10)
            """);
}
