using Demizon.Mvc.Services.Costume;

namespace Demizon.Tests.Unit;

public class CostumeScanTests
{
    [Theory]
    [InlineData("https://demizon.cz/k/K0042", "K0042")]
    [InlineData("https://jina-domena.cz/k/K0042", "K0042")]
    [InlineData("  K0042 ", "K0042")]
    [InlineData("k0042", "k0042")]
    public void Kod_kusu_se_vezme_z_posledniho_segmentu_nebo_z_textu(string text, string code)
    {
        var scan = CostumeScan.Parse(text);

        Assert.Equal(new CostumeScan(CostumeScanKind.Item, code), scan);
    }

    [Theory]
    [InlineData("https://demizon.cz/m/AbC-12_xY")]
    [InlineData("https://demizon.cz/M/AbC-12_xY")]
    [InlineData("m/AbC-12_xY")]
    public void QR_clena_se_pozna_podle_segmentu_m(string text)
    {
        var scan = CostumeScan.Parse(text);

        Assert.Equal(new CostumeScan(CostumeScanKind.Member, "AbC-12_xY"), scan);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://demizon.cz/")]
    public void Prazdny_vstup_neni_nic(string? text)
    {
        Assert.Null(CostumeScan.Parse(text));
    }
}
