using Demizon.Core.Services.Costume;
using Demizon.Dal.Entities;
using Demizon.Mvc.Services.Costume;

namespace Demizon.Tests.Unit;

/// <summary>Komu krojárka posílá push. Autor akce nikdy nedostane zprávu o tom, co sám udělal.</summary>
public class CostumeNotificationsTests
{
    private const int Keeper = 1;
    private const int Jana = 2;
    private const int Petr = 3;
    private const int SecondKeeper = 4;

    private static readonly IReadOnlyDictionary<int, CostumeMemberName> Names = new Dictionary<int, CostumeMemberName>
    {
        [Keeper] = new(Keeper, "Krojařka Marie", false),
        [Jana] = new(Jana, "Jana Nová", false),
        [Petr] = new(Petr, "Petr Starý", false),
    };

    private static CostumeTransfer Transfer(int? from, int? to, int createdBy, params string[] codes) => new()
    {
        Id = 42, FromMemberId = from, ToMemberId = to, CreatedById = createdBy,
        Lines = codes.Select(c => new CostumeTransferLine { Item = new CostumeItem { Code = c }, Quantity = 1 }).ToList(),
    };

    [Fact]
    public void Odeslany_vydej_dostane_jen_prijemce_s_odkazem_na_predavku()
    {
        var pushes = CostumeNotifications.ForSubmitted(Transfer(null, Jana, Keeper, "K0001"),
            CostumeTransferStatus.Pending, Keeper, Names);

        var push = Assert.Single(pushes);
        Assert.Equal(Jana, push.MemberId);
        Assert.Contains("Krojárka ti předává kus K0001", push.Body);
        Assert.Equal("42", push.Data[CostumeNotifications.TransferIdKey]);
    }

    [Fact]
    public void Vraceni_potvrzene_odeslanim_dostane_vracejici()
    {
        var pushes = CostumeNotifications.ForSubmitted(Transfer(Jana, null, Keeper, "K0001", "K0002"),
            CostumeTransferStatus.Confirmed, Keeper, Names);

        var push = Assert.Single(pushes);
        Assert.Equal(Jana, push.MemberId);
        Assert.Contains("2 kusů (K0001, K0002)", push.Body);
    }

    [Fact]
    public void Potvrzene_predani_mezi_cleny_vidi_zakladatel_i_spravci()
    {
        var pushes = CostumeNotifications.ForConfirmed(Transfer(Jana, Petr, Jana, "K0001"), Petr,
            [Keeper, SecondKeeper], Names);

        Assert.Equal([Jana, Keeper, SecondKeeper], pushes.Select(p => p.MemberId));
        Assert.Contains("Petr Starý potvrdil(a)", pushes[0].Body);
        Assert.Contains("Jana Nová → Petr Starý", pushes[1].Body);
    }

    [Fact]
    public void Potvrzeny_vydej_z_krojarky_dostane_jen_zakladatel_bez_zpravy_spravcum_o_predani()
    {
        var pushes = CostumeNotifications.ForConfirmed(Transfer(null, Jana, Keeper, "K0001"), Jana,
            [Keeper, SecondKeeper], Names);

        var push = Assert.Single(pushes);
        Assert.Equal(Keeper, push.MemberId);
    }

    [Fact]
    public void Odmitnuti_dostane_zakladatel_a_spravci_kazdy_jednou_s_duvodem()
    {
        var transfer = Transfer(null, Jana, Keeper, "K0001");
        transfer.RejectionReason = "Chybí knoflík";

        var pushes = CostumeNotifications.ForRejected(transfer, Jana, [Keeper, SecondKeeper], Names);

        Assert.Equal([Keeper, SecondKeeper], pushes.Select(p => p.MemberId));
        Assert.All(pushes, p => Assert.Contains("Chybí knoflík", p.Body));
    }

    [Fact]
    public void Pripominka_vyjmenuje_kusy_a_vezme_vlastni_text()
    {
        CostumeHolding Holding(string code, int quantity) =>
            new() { MemberId = Jana, Quantity = quantity, Item = new CostumeItem { Code = code } };

        var push = CostumeNotifications.Reminder(Jana, [Holding("S0002", 3), Holding("K0001", 1)], "Do neděle prosím.");

        Assert.Equal("Do neděle prosím. Máš u sebe 4 ks: K0001, S0002.", push.Body);
        Assert.Equal("mine", push.Data[CostumeNotifications.MyCostumesKey]);
    }
}
