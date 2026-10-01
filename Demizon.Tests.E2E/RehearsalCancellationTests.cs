using Demizon.Tests.E2E.Infrastructure;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Demizon.Tests.E2E;

/// <summary>
/// Zrušení zkoušky z administrace: dialog musí projít validací, zrušená
/// zkouška se musí objevit v seznamu výjimek i v tabulce docházky a jít
/// obnovit. Test po sobě uklízí, aplikace je sdílená celou kolekcí.
/// </summary>
public class RehearsalCancellationTests(E2EFixture fixture) : E2ETestBase(fixture)
{
    private const string Reason = "E2E hody";

    [Fact]
    public async Task Admin_zrusi_zkousku_a_zase_ji_obnovi()
    {
        await LoginAsAdminAsync();
        await Page.GotoAsync("/Admin/Rehearsals", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(Page.GetByText("Nadcházející zkoušky")).ToBeVisibleAsync(new() { Timeout = 20_000 });

        // Zrušit první nadcházející zkoušku: dialog je předvyplněný jejím dnem.
        await Page.Locator("button[title='Zrušit tuto zkoušku']").First.ClickAsync();
        var dialog = Page.Locator(".mud-dialog").First;
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10_000 });
        await dialog.GetByLabel("Důvod (uvidí ho členové)").FillAsync(Reason);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Zrušit zkoušky" }).ClickAsync();

        await Expect(Page.GetByText("Zkoušky zrušeny.")).ToBeVisibleAsync(new() { Timeout = 10_000 });
        // Řádek výjimky, ne řádek nadcházející zkoušky (ten důvod ukazuje taky).
        var cancellationRow = Page.Locator("tr:has(button[title='Obnovit zkoušky'])", new() { HasText = Reason });
        await Expect(cancellationRow).ToBeVisibleAsync();

        // V tabulce docházky je sloupec označený „odpadá“ a nedá se vyplnit.
        await Page.GotoAsync("/Admin/MemberAttendance/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Expect(Page.Locator(".demi-att-table")).ToBeVisibleAsync(new() { Timeout = 20_000 });
        var cancelledColumn = Page.Locator("th.demi-att-col-cancelled", new() { HasText = "odpadá" });
        if (await cancelledColumn.CountAsync() == 0)
        {
            // Na konci měsíce je nejbližší pátek už v příštím.
            await Page.Locator("button[title='Následující měsíc']").ClickAsync();
        }
        await Expect(cancelledColumn.First).ToBeVisibleAsync(new() { Timeout = 10_000 });

        // Obnovit a ověřit, že výjimka zmizela.
        await Page.GotoAsync("/Admin/Rehearsals", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await cancellationRow.Locator("button[title='Obnovit zkoušky']").ClickAsync();
        await Page.Locator(".mud-dialog")
            .GetByRole(AriaRole.Button, new() { Name = "Obnovit", Exact = true }).ClickAsync();

        await Expect(Page.GetByText("Zkoušky obnoveny.")).ToBeVisibleAsync(new() { Timeout = 10_000 });
        await Expect(Page.Locator("tr", new() { HasText = Reason })).ToHaveCountAsync(0);

        Assert.Empty(RelevantConsoleErrors);
    }
}
