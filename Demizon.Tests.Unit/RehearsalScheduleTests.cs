using Demizon.Common.Configuration;
using Demizon.Core.Services.Rehearsal;

namespace Demizon.Tests.Unit;

/// <summary>
/// Říjen 2026 má pět pátků (2., 9., 16., 23., 30.), listopad čtyři (6., 13., 20., 27.).
/// Letní čas končí v neděli 25. 10. 2026.
/// </summary>
public class RehearsalScheduleTests
{
    private static readonly RehearsalRule Rule = RehearsalRule.From(new RehearsalSettings(), "Europe/Prague");

    private static DateOnly D(int month, int day) => new(2026, month, day);

    private static DateTime T(int month, int day, int hour = 0, int minute = 0) =>
        new(2026, month, day, hour, minute, 0);

    private static IReadOnlyList<RehearsalOccurrence> Compute(
        DateOnly from, DateOnly toExclusive,
        EventSpan[]? events = null,
        CancellationSpan[]? cancellations = null) =>
        RehearsalSchedule.Compute(from, toExclusive, Rule, events ?? [], cancellations ?? []);

    private static IReadOnlyList<RehearsalOccurrence> October(
        EventSpan[]? events = null, CancellationSpan[]? cancellations = null) =>
        Compute(D(10, 1), D(11, 1), events, cancellations);

    private static int[] Days(IEnumerable<RehearsalOccurrence> occurrences) =>
        occurrences.Select(o => o.Date.Day).ToArray();

    // ---------------------------------------------------------------- pátky v rozsahu

    [Fact]
    public void Mesic_s_peti_patky_ma_pet_zkousek()
    {
        Assert.Equal([2, 9, 16, 23, 30], Days(October()));
    }

    [Fact]
    public void Mesic_se_ctyrmi_patky_ma_ctyri_zkousky()
    {
        Assert.Equal([6, 13, 20, 27], Days(Compute(D(11, 1), D(12, 1))));
    }

    [Fact]
    public void Konec_rozsahu_je_exkluzivni_a_zacatek_vcetne()
    {
        Assert.Equal([2], Days(Compute(D(10, 2), D(10, 9))));
    }

    [Fact]
    public void Zkouska_zacina_v_19_a_trva_dve_a_pul_hodiny()
    {
        var occurrence = October()[0];

        Assert.Equal(T(10, 2, 19), occurrence.StartsAt);
        Assert.Equal(T(10, 2, 21, 30), occurrence.EndsAt);
        Assert.Equal(T(10, 2), occurrence.Day);
        Assert.Equal(RehearsalState.Scheduled, occurrence.State);
    }

    [Fact]
    public void Prechod_z_letniho_casu_posune_zacatek_v_UTC()
    {
        var occurrences = October();
        var beforeChange = occurrences.Single(o => o.Date == D(10, 23));
        var afterChange = occurrences.Single(o => o.Date == D(10, 30));

        // Místní čas je pořád 19:00, v UTC je to jednou 17:00 (CEST) a jednou 18:00 (CET).
        Assert.Equal(new DateTime(2026, 10, 23, 17, 0, 0), beforeChange.StartsAtUtc);
        Assert.Equal(new DateTime(2026, 10, 30, 18, 0, 0), afterChange.StartsAtUtc);
        Assert.Equal(new DateTime(2026, 10, 30, 20, 30, 0), afterChange.EndsAtUtc);
    }

    // ---------------------------------------------------------------- akce zkoušku schová

    [Fact]
    public void Jednodenni_akce_v_patek_zkousku_schova()
    {
        var occurrences = October([new EventSpan(T(10, 9, 18), T(10, 9, 22), false)]);

        Assert.Equal([2, 16, 23, 30], Days(occurrences));
    }

    [Fact]
    public void Akce_zadana_jen_datem_v_patek_zkousku_schova()
    {
        // Blazor ukládá jednodenní akci jako DateFrom == DateTo == půlnoc.
        var occurrences = October([new EventSpan(T(10, 9), T(10, 9), false)]);

        Assert.DoesNotContain(9, Days(occurrences));
    }

    [Fact]
    public void Vicedenni_akce_ctvrtek_az_nedele_schova_patek_mezi()
    {
        // Dřív se kontrolovalo jen DateFrom.Date == pátek, takže tahle akce zkoušku neschovala.
        var occurrences = October([new EventSpan(T(10, 15, 17), T(10, 18, 16), false)]);

        Assert.Equal([2, 9, 23, 30], Days(occurrences));
    }

    [Fact]
    public void Akce_zadana_jen_datem_ctvrtek_az_patek_zabira_i_patek()
    {
        // Blazor date range picker: konec je půlnoc posledního dne, a ten do akce patří.
        var occurrences = October([new EventSpan(T(10, 15), T(10, 16), false)]);

        Assert.DoesNotContain(16, Days(occurrences));
    }

    [Fact]
    public void Akce_s_casem_koncici_v_pulnoc_do_patku_nezasahuje()
    {
        var occurrences = October([new EventSpan(T(10, 15, 18), T(10, 16), false)]);

        Assert.Contains(16, Days(occurrences));
    }

    [Fact]
    public void Akce_v_patek_po_pulnoci_zkousku_schova()
    {
        // Časy akcí jsou místní. 00:30 v pátek je pátek, žádný posun do čtvrtka.
        var occurrences = October([new EventSpan(T(10, 9, 0, 30), T(10, 9, 3), false)]);

        Assert.DoesNotContain(9, Days(occurrences));
    }

    [Fact]
    public void Akce_ve_ctvrtek_vecer_zkousku_neschova()
    {
        var occurrences = October([new EventSpan(T(10, 8, 22), T(10, 8, 23, 59), false)]);

        Assert.Contains(9, Days(occurrences));
    }

    [Fact]
    public void Zrusena_akce_zkousku_vrati()
    {
        var occurrences = October([new EventSpan(T(10, 9, 18), T(10, 9, 22), IsCancelled: true)]);

        Assert.Contains(9, Days(occurrences));
    }

    [Fact]
    public void Akce_s_koncem_pred_zacatkem_zabira_aspon_svuj_prvni_den()
    {
        var occurrences = October([new EventSpan(T(10, 9, 18), T(10, 8, 18), false)]);

        Assert.DoesNotContain(9, Days(occurrences));
    }

    // ---------------------------------------------------------------- výjimky

    [Fact]
    public void Zruseni_jednoho_dne_zkousku_oznaci_a_nesmaze()
    {
        var occurrences = October(cancellations: [new CancellationSpan(7, D(10, 9), D(10, 9), "Hody")]);

        Assert.Equal([2, 9, 16, 23, 30], Days(occurrences));
        var cancelled = occurrences.Single(o => o.IsCancelled);
        Assert.Equal(D(10, 9), cancelled.Date);
        Assert.Equal(7, cancelled.CancellationId);
        Assert.Equal("Hody", cancelled.CancellationReason);
        Assert.All(occurrences.Where(o => o.Date != D(10, 9)), o =>
        {
            Assert.Equal(RehearsalState.Scheduled, o.State);
            Assert.Null(o.CancellationId);
            Assert.Null(o.CancellationReason);
        });
    }

    [Fact]
    public void Zruseni_rozsahu_pres_hranici_mesice_plati_v_obou_mesicich()
    {
        var holidays = new CancellationSpan(1, D(10, 25), D(11, 8), "Prázdniny");

        var october = October(cancellations: [holidays]);
        var november = Compute(D(11, 1), D(12, 1), cancellations: [holidays]);

        Assert.Equal([30], Days(october.Where(o => o.IsCancelled)));
        Assert.Equal([6], Days(november.Where(o => o.IsCancelled)));
    }

    [Fact]
    public void Uzsi_vyjimka_ma_prednost_pred_sirsi()
    {
        var occurrences = October(cancellations:
        [
            new CancellationSpan(1, D(10, 1), D(10, 31), "Prázdniny"),
            new CancellationSpan(2, D(10, 16), D(10, 16), "Hody"),
        ]);

        Assert.Equal("Hody", occurrences.Single(o => o.Date == D(10, 16)).CancellationReason);
        Assert.Equal("Prázdniny", occurrences.Single(o => o.Date == D(10, 23)).CancellationReason);
    }

    [Fact]
    public void Den_s_akci_neni_zkouska_ani_kdyz_je_zaroven_zruseny()
    {
        var occurrences = October(
            [new EventSpan(T(10, 9, 18), T(10, 9, 22), false)],
            [new CancellationSpan(1, D(10, 9), D(10, 9), null)]);

        Assert.DoesNotContain(9, Days(occurrences));
    }

    [Fact]
    public void Pravidlo_z_nastaveni_umi_jiny_den_a_cas()
    {
        var rule = RehearsalRule.From(
            new RehearsalSettings
            {
                DayOfWeek = DayOfWeek.Wednesday,
                StartTime = new TimeSpan(18, 0, 0),
                Duration = new TimeSpan(1, 0, 0),
            },
            "Europe/Prague");

        var occurrences = RehearsalSchedule.Compute(D(10, 1), D(10, 15), rule, [], []);

        Assert.Equal([7, 14], Days(occurrences));
        Assert.Equal(T(10, 7, 19), occurrences[0].EndsAt);
    }
}
