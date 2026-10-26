using Demizon.Common.Configuration;
using Demizon.Core.Services.Costume;
using Demizon.Dal;
using Demizon.Dal.Entities;
using Demizon.Tests.E2E.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using static Microsoft.Playwright.Assertions;

namespace Demizon.Tests.E2E;

/// <summary>
/// Krojárka v prohlížeči: správce plní katalog a tiskne štítky, člen potvrdí převzetí podle fotek.
/// Aplikace je sdílená celou kolekcí, proto každý test používá vlastní kódy kusů.
/// </summary>
public class CostumeStoreTests(E2EFixture fixture) : E2ETestBase(fixture)
{
    private Task LoginAsKeeperAsync() => LoginAsync(AppHost.KeeperLogin, AppHost.KeeperPassword);

    private CostumeTransferService Transfers(DemizonContext db) =>
        new(db, new CostumePhotoProcessor(db, Options.Create(new CostumeSettings()),
                Options.Create(new GoogleCalendarSettings { RedirectUri = "http://localhost" }),
                NullLogger<CostumePhotoProcessor>.Instance),
            NullLogger<CostumeTransferService>.Instance);

    private static MemoryStream Jpeg()
    {
        using var image = new Image<Rgb24>(64, 48);
        var stream = new MemoryStream();
        image.SaveAsJpeg(stream);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public async Task Spravce_zalozi_soucastku_a_kus_a_vytiskne_stitek()
    {
        const string part = "E2E vesta";
        const string code = "E2EV1";
        await LoginAsKeeperAsync();
        await Page.GotoAsync("/Admin/Costumes/Catalog", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        await Page.GetByRole(AriaRole.Button, new() { Name = "Součástka" }).ClickAsync();
        var dialog = Page.Locator(".mud-dialog").First;
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10_000 });
        await dialog.GetByLabel("Název").FillAsync(part);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Vytvořit" }).ClickAsync();
        await Expect(Page.GetByText("Součástka vytvořena.")).ToBeVisibleAsync(new() { Timeout = 10_000 });

        var partHeader = Page.Locator(".costume-part-header", new() { Has = Page.GetByText(part, new() { Exact = true }) });
        await partHeader.GetByRole(AriaRole.Button, new() { Name = "Kus" }).ClickAsync();
        dialog = Page.Locator(".mud-dialog").First;
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10_000 });
        await dialog.GetByLabel("Inventární kód").FillAsync(code.ToLowerInvariant());
        await dialog.GetByLabel("Velikost").FillAsync("4");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Vytvořit" }).ClickAsync();
        await Expect(Page.GetByText("Kus vytvořen.")).ToBeVisibleAsync(new() { Timeout = 10_000 });
        // Kód se uloží velkými písmeny bez ohledu na to, jak ho správce napsal.
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = code, Exact = true })).ToBeVisibleAsync();

        await Page.GotoAsync($"/Admin/Costumes/Labels?codes={code}", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        var label = Page.Locator(".costume-label").First;
        await Expect(label.Locator("svg")).ToBeVisibleAsync(new() { Timeout = 10_000 });
        await Expect(label.Locator(".code")).ToHaveTextAsync(code);

        Assert.Empty(RelevantConsoleErrors);
    }

    [Fact]
    public async Task Clen_zkontroluje_fotku_potvrdi_prevzeti_a_kus_ma_u_sebe()
    {
        const string code = "E2EP1";
        using (var db = Fixture.App.NewContext())
        {
            var keeper = db.Members.Single(m => m.Login == AppHost.KeeperLogin);
            var member = db.Members.Single(m => m.Login == AppHost.MemberLogin);
            db.CostumeItems.Add(new CostumeItem
            {
                Code = code, Size = "106",
                Part = new CostumePart { Name = "E2E nohavice", Gender = CostumeGender.Male, TrackingMode = CostumeTrackingMode.Individual },
            });
            db.SaveChanges();

            var actor = new CostumeActor(keeper.Id, IsKeeper: true);
            var transfers = Transfers(db);
            var id = (await transfers.CreateDraftAsync(actor, null, member.Id, [new(code)], null)).Value;
            var lineId = (await transfers.GetAsync(id))!.Lines.Single().Id;
            Assert.True((await transfers.AddPhotoAsync(id, lineId, actor, Jpeg())).IsSuccess);
            Assert.True((await transfers.SubmitAsync(id, actor)).IsSuccess);
        }

        await LoginAsMemberAsync();
        await Page.GotoAsync("/Admin/Costumes", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Page.GetByRole(AriaRole.Link, new() { Name = "Zkontrolovat a potvrdit" }).First.ClickAsync();

        await Expect(Page.Locator("img[alt='Fotka stavu']").First).ToBeVisibleAsync(new() { Timeout = 10_000 });
        await Page.GetByRole(AriaRole.Button, new() { Name = "Potvrzuji převzetí" }).ClickAsync();
        await Expect(Page.GetByText("Převzetí potvrzeno.")).ToBeVisibleAsync(new() { Timeout = 10_000 });

        await Page.GotoAsync("/Admin/Costumes", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = code, Exact = true })).ToBeVisibleAsync(new() { Timeout = 10_000 });

        // Adresa ze štítku ukáže, u koho kus je.
        await Page.GotoAsync($"/k/{code}", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(Page.GetByText("vel. 106")).ToBeVisibleAsync(new() { Timeout = 10_000 });
        await Expect(Page.GetByText(": 1 ks")).ToBeVisibleAsync();

        Assert.Empty(RelevantConsoleErrors);
    }

    [Fact]
    public async Task Bezny_clen_katalog_neotevre_ale_vidi_svuj_QR()
    {
        await LoginAsMemberAsync();

        await Page.GotoAsync("/Admin/Costumes", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(Page.GetByText("Můj QR pro krojárku")).ToBeVisibleAsync(new() { Timeout = 10_000 });
        await Expect(Page.Locator("svg").First).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Katalog" })).ToHaveCountAsync(0);

        await Page.GotoAsync("/Admin/Costumes/Catalog", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        Assert.DoesNotContain("Štítky (", await Page.InnerTextAsync("body"));
    }

    [Theory]
    [InlineData("/Admin/Costumes")]
    [InlineData("/Admin/Costumes/Catalog")]
    [InlineData("/Admin/Costumes/Holders")]
    public async Task Stranky_krojarky_na_mobilu_nepretecou(string path)
    {
        await using var mobile = await Fixture.NewContextAsync(390, 844);
        var page = await mobile.NewPageAsync();
        await page.GotoAsync("/Login", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.FillAsync("input[name='Login']", AppHost.KeeperLogin);
        await page.FillAsync("input[name='Password']", AppHost.KeeperPassword);
        await page.ClickAsync("button[type='submit']");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await page.GotoAsync(path, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        var (overflow, culprits) = await MeasureOverflowAsync(page);

        Assert.True(overflow <= 2, $"{path} přetéká na 390 px o {overflow} px. Viníci: {string.Join(" | ", culprits)}");
    }
}
